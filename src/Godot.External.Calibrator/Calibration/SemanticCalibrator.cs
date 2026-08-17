using LiveClr.Calibration;

namespace Godot.External.Calibrator.Calibration;

/// <summary>One object's fetched window, tagged with the address it came from.</summary>
public sealed record NodeSample(ulong Address, MemoryWindow Window);

/// <summary>
/// §8.9 (b): the semantic half. Known values in, candidate offsets out, intersected.
/// </summary>
/// <remarks>
/// <para>
/// Every method here returns candidates rather than an offset, and none of them will ever return a
/// determined answer from one object — that is the §12.5 result restated as a type. Probe 15 scanned
/// a single 200×50 control for its size and got four candidates (<c>0x4c0</c>, <c>0x4c8</c>,
/// <c>0x4d4</c>, <c>0x4f4</c>); intersecting with one control of a <em>different</em> size left
/// exactly one.
/// </para>
/// <para>
/// The scan stride is four bytes even in a double-precision build. <c>0x4d4</c> in that candidate
/// list is not 8-aligned, so a natural-alignment stride would have hidden a real collision rather
/// than removing it, and hidden collisions are how an intersection ends up unique and wrong.
/// </para>
/// </remarks>
public sealed class SemanticCalibrator
{
    /// <summary>Comparison slack, matching the harness's own <c>EPS</c>.</summary>
    public const double Epsilon = 1e-3;

    private const int Stride = 4;

    private readonly GodotPrecisionWidth _precision;

    /// <summary>Creates a calibrator for a build of the given <c>real_t</c> width.</summary>
    public SemanticCalibrator(GodotPrecisionWidth precision) => _precision = precision;

    /// <summary>
    /// <c>Control::Data::size_cache</c>: the adjacent <c>real_t</c> pair equal to a known size.
    /// </summary>
    /// <remarks>
    /// The only field here derived from ground truth the harness states outright. Adjacency does
    /// real work — <c>887.0</c> alone recurs all over a live UI object, the ordered pair
    /// <c>(887, 313)</c> does not.
    /// </remarks>
    public OffsetCandidates DeriveSize(IReadOnlyList<(NodeSample Sample, double Width, double Height)> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return IntersectPerSample(
            "control.size (adjacent real_t pair == known size)",
            samples.Select(s => (s.Sample, Expectation: (s.Width, s.Height), Candidates: PairCandidates(s.Sample.Window, s.Width, s.Height, 0, int.MaxValue))));
    }

    /// <summary>
    /// <c>Control::Data::offset[4]</c>, from the same known sizes and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The known value is a <em>difference</em>, not a stored number: a control's raw offsets
    /// satisfy <c>offset[2] - offset[0] == size.x</c> and <c>offset[3] - offset[1] == size.y</c>
    /// whenever its opposing anchors are equal, which covers a zero-anchored control and the grid's
    /// 0.5-anchored one alike. So <c>offset[4]</c> is derivable from the sizes the harness supplies
    /// without ever being told a single offset value.
    /// </para>
    /// <para>
    /// This is also what disarms the §4.6 trap. <c>Control::Data</c> puts <c>anchor[4]</c>
    /// immediately after <c>offset[4]</c>, and on a scene where every anchor is zero the two are
    /// indistinguishable by shape — but an anchor quad differences to <c>(0, 0)</c>, never to the
    /// size, so it can never enter this candidate set. Neither can the resolved rect
    /// <c>[x, y, w, h]</c>, which differences to <c>(w - x, h - y)</c>.
    /// </para>
    /// </remarks>
    public OffsetCandidates DeriveOffsetQuad(
        IReadOnlyList<(NodeSample Sample, double Width, double Height)> samples,
        int excludeOffset)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return IntersectPerSample(
            "control.offset (real_t[4] whose opposing differences == known size)",
            samples.Select(s => (s.Sample, Expectation: (s.Width, s.Height), Candidates: QuadCandidates(s.Sample.Window, s.Width, s.Height, excludeOffset))));
    }

    /// <summary>
    /// <c>Control::Data::pos_cache</c>, anchored on the already-derived <c>offset[4]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The harness supplies no positions, so the known value has to be manufactured: for a control
    /// whose leading anchors are zero, <c>pos_cache == (offset[0], offset[1])</c>, and
    /// <c>offset[4]</c> has already been derived. Those pairs are per-node and mostly distinct, which
    /// is exactly the input an intersection wants.
    /// </para>
    /// <para>
    /// A node with non-zero anchors breaks the identity on purpose — the grid's <c>AnchoredWide</c>
    /// sits at <c>(12.5, -40.5)</c> while its raw offsets start at <c>(-431, -197)</c>. A strict
    /// intersection would therefore come back empty, so support is counted instead of required
    /// unanimously, and the dissenting nodes are handed back in
    /// <paramref name="dissenting"/> for the caller to explain rather than ignore: the residual
    /// <c>pos - offset[0..1]</c> should be <c>anchor * parent_size</c>, and if it is not, the offset
    /// is wrong rather than the node being anchored.
    /// </para>
    /// </remarks>
    public OffsetCandidates DerivePosition(
        IReadOnlyList<(NodeSample Sample, double X, double Y)> samples,
        int excludeOffset,
        out IReadOnlyList<ulong> dissenting)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return BestSupported(
            "control.position (real_t pair == derived offset[0..1])",
            samples.Select(s => (s.Sample, Expectation: (s.X, s.Y), Candidates: PairCandidates(s.Sample.Window, s.X, s.Y, 0, int.MaxValue).Where(c => c != excludeOffset).ToArray())).ToList(),
            requireVariation: false,
            out dissenting);
    }

    /// <summary>
    /// <c>Control::Data::scale</c>, from the engine's documented default and from the fact that it
    /// varies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the weakest derivation in the calibrator and it is labelled as such wherever it is
    /// reported.</b> The harness states no scales, so the only known value available is upstream's
    /// declared default <c>Vector2 scale = Vector2(1, 1)</c> — engine semantics rather than an
    /// offset table, but an assumption about the scene all the same.
    /// </para>
    /// <para>
    /// One anchor of <c>(1, 1)</c> is nowhere near enough on its own: <c>CanvasItem::modulate</c> and
    /// <c>self_modulate</c> are <c>Color(1,1,1,1)</c>, which offers six more adjacent
    /// <c>(1.0, 1.0)</c> pairs per node. Two constraints separate them.
    /// </para>
    /// <list type="number">
    /// <item><b>Region.</b> Only offsets strictly between the derived <c>control.offset</c> and
    /// <c>control.position</c> are considered. <c>Control</c> derives from <c>CanvasItem</c>, and
    /// single inheritance lays a base class out first, so every modulate-like field is below the
    /// whole <c>Control::Data</c> block by construction rather than by luck.</item>
    /// <item><b>Variation.</b> The true scale differs on at least one node in any scene that has a
    /// scaled control; a constant is a constant. Where nothing in the scene is scaled this cannot
    /// separate them, and the result is reported ambiguous instead of picked.</item>
    /// </list>
    /// </remarks>
    public OffsetCandidates DeriveScale(
        IReadOnlyList<NodeSample> samples,
        double defaultX,
        double defaultY,
        int regionLowExclusive,
        int regionHighExclusive)
    {
        ArgumentNullException.ThrowIfNull(samples);

        List<(NodeSample Sample, (double, double) Expectation, int[] Candidates)> perSample = [];
        foreach (NodeSample sample in samples)
        {
            perSample.Add((
                sample,
                (defaultX, defaultY),
                PairCandidates(sample.Window, defaultX, defaultY, regionLowExclusive + 1, regionHighExclusive)));
        }

        return BestSupported(
            "control.scale (real_t pair == engine default (1,1), inside Control::Data, and not constant)",
            perSample,
            requireVariation: true,
            out _);
    }

    /// <summary>
    /// <c>CanvasItem::visible</c>: a byte that is 1 on the visible twin, 0 on the hidden one, and
    /// boolean-valued on every node in between.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two samples with two distinct expected values is the minimum the gate accepts and exactly
    /// what the twins provide. Everything that narrows it further is <c>CanvasItem</c>'s declared
    /// layout, which is identical in 4.3 and 4.5 and carries no <c>#ifdef</c>s:
    /// </para>
    /// <code>
    /// V-16 int z_index | V-12 bool z_relative | V-11 bool y_sort_enabled | V-10..-9 padding
    /// V-8  Window *window        &lt;- a POINTER, and the discriminator that settles it
    /// V+0  bool visible          &lt;- the stored property; visible-in-tree is computed, never stored
    /// V+1  bool parent_visible_in_tree
    /// V+2  bool pending_update   &lt;- flips every frame
    /// V+3..V+10 more bools | V+11 padding(0) | V+12 clip_children_mode (u32, in {0,1,2})
    /// </code>
    /// <para>
    /// <b>There is deliberately no requirement that the byte hold still between two readings.</b>
    /// That test was tried and it discriminates against the correct answer: in a live UI
    /// <c>visible</c> genuinely toggles as cards, tooltips and panels animate, so the real field was
    /// rejected as unstable while the stable decoys were rejected for not differing across the
    /// twins — leaving nothing at all. That is the "never wrong, always absent" signature, and no
    /// amount of timing fixes it. Structure needs no timing.
    /// </para>
    /// <para>
    /// The pair semantics matter too. <c>visible</c> only differs between the twins if the hidden one
    /// was hidden by its own <c>hide()</c>. Hide it by hiding an <em>ancestor</em> and the stored
    /// <c>visible</c> is identical on both, while the byte that differs is
    /// <c>parent_visible_in_tree</c> one byte later. So a difference at <c>D</c> nominates both
    /// <c>D</c> and <c>D-1</c>, and the alignment rule decides between them.
    /// </para>
    /// </remarks>
    public OffsetCandidates DeriveVisible(
        NodeSample visible,
        NodeSample hidden,
        IReadOnlyList<NodeSample> allNodes,
        int upperBoundExclusive,
        ICollection<string>? diagnostics = null,
        int lowerBoundExclusive = 0)
    {
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(hidden);
        ArgumentNullException.ThrowIfNull(allNodes);

        int limit = Math.Min(upperBoundExclusive, Math.Min(visible.Window.Length, hidden.Window.Length));
        SortedSet<int> nominated = [];

        for (int offset = 0; offset < limit; offset++)
        {
            if (visible.Window.TryByte(offset, out byte on) && on == 1
                && hidden.Window.TryByte(offset, out byte off) && off == 0)
            {
                nominated.Add(offset);      // hidden by its own hide(): the difference IS visible
                nominated.Add(offset - 1);  // hidden by an ancestor: the difference is at V+1
            }
        }

        List<int> candidates = [];
        foreach (int candidate in nominated)
        {
            // Single inheritance gives a two-sided bound from offsets the STRUCTURAL pass already
            // derived by pointer identity: node.name < canvasItem.visible < control.offset. Both
            // ends are unique on a single sample and cannot be vetoed by a live UI, which is what
            // makes this the right shape of constraint here.
            if (candidate <= lowerBoundExclusive)
            {
                diagnostics?.Add($"0x{candidate:x} rejected: at or below the derived Node members "
                               + $"(0x{lowerBoundExclusive:x}), so it cannot be a CanvasItem field");
                continue;
            }

            bool? onVisible = Examine(visible.Window, candidate, out string why);
            if (onVisible is not true)
            {
                // Recorded rather than dropped: when this list comes back empty, the reason the
                // TRUE offset was eliminated is the only thing worth knowing, and guessing at it
                // from the outside costs a whole matrix run.
                diagnostics?.Add($"0x{candidate:x} rejected on the visible twin: "
                               + (onVisible is null ? "window could not be read there" : why));
                continue;
            }

            if (Examine(hidden.Window, candidate, out why) is not true)
            {
                diagnostics?.Add($"0x{candidate:x} rejected on the hidden twin: {why}");
                continue;
            }

            // NO universality rule. Requiring every other sampled Control to agree let any single
            // unrelated node veto the true answer — measured once, on a cell where 0x370 was
            // nominated and then eliminated by one other Control. The inheritance bracket above is
            // strictly weaker and cannot be vetoed by anything.
            candidates.Add(candidate);
        }

        // Coverage is asked of the samples the derivation RESTS on — the two twins, over the range
        // actually scanned. The other nodes only ever narrow the answer, so a byte they could not
        // supply removed no candidate and hid nothing. Demanding a complete window from all of them
        // was not a stronger §12.5 gate, it was a different and unrelated requirement, and it is why
        // this offset was withheld on 11 of 24 runs while never once being wrong.
        bool coverage = visible.Window.IsReadable(0, limit) && hidden.Window.IsReadable(0, limit);
        return new OffsetCandidates(
            "canvasItem.visible (byte: 1 on the visible twin, 0 on the hidden one, boolean on all nodes)",
            candidates,
            CalibrationTechnique.Semantic,
            // The twins, and only the twins. The other nodes no longer constrain the answer, so
            // counting them as samples would overstate what this rests on.
            [visible.Address, hidden.Address],
            ["visible=true", "visible=false"],
            coverage);
    }

    private int[] PairCandidates(MemoryWindow window, double first, double second, int lowInclusive, int highExclusive)
    {
        List<int> candidates = [];
        int last = Math.Min(highExclusive - 1, window.Length - (2 * _precision.Size));
        int start = Math.Max(0, lowInclusive);
        start += (Stride - (start % Stride)) % Stride;
        for (int offset = start; offset <= last; offset += Stride)
        {
            if (window.TryReal(offset, _precision, out double x)
                && window.TryReal(offset + _precision.Size, _precision, out double y)
                && Near(x, first)
                && Near(y, second))
            {
                candidates.Add(offset);
            }
        }

        return [.. candidates];
    }

    private int[] QuadCandidates(MemoryWindow window, double width, double height, int excludeOffset)
    {
        List<int> candidates = [];
        Span<double> quad = stackalloc double[4];
        int last = window.Length - (4 * _precision.Size);
        for (int offset = 0; offset <= last; offset += Stride)
        {
            if (offset == excludeOffset || !window.TryReals(offset, _precision, 4, quad))
            {
                continue;
            }

            if (!Plausible(quad[0]) || !Plausible(quad[1]) || !Plausible(quad[2]) || !Plausible(quad[3]))
            {
                continue;
            }

            if (Near(quad[2] - quad[0], width) && Near(quad[3] - quad[1], height))
            {
                candidates.Add(offset);
            }
        }

        return [.. candidates];
    }

    /// <summary>
    /// Whether a <c>real_t</c> reading is a coordinate at all.
    /// </summary>
    /// <remarks>
    /// §12.4c: Control accessors on a non-Control return denormals such as <c>2.6e-38</c>, because an
    /// x64 heap pointer's high half decodes as a near-denormal float and its low half as a wildly
    /// out-of-range one. Infinity and NaN arrive the same way. This is the test that keeps such a
    /// reading out of a result instead of publishing it as geometry.
    /// </remarks>
    public static bool IsPlausibleReading(double value)
        => double.IsFinite(value) && Math.Abs(value) <= 1e7 && (value == 0 || Math.Abs(value) >= 1e-3);

    /// <summary>
    /// Whether the bytes around <paramref name="candidate"/> are shaped like <c>CanvasItem</c>'s
    /// <c>visible</c>. <see langword="null"/> means the window could not answer.
    /// </summary>
    public static bool? FitsCanvasItem(MemoryWindow window, int candidate)
    {
        bool? verdict = Examine(window, candidate, out _);
        return verdict;
    }

    /// <summary>
    /// The same test, with the reason a candidate was rejected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two rules, both of them facts about the ABI rather than about this build's field order:
    /// </para>
    /// <list type="number">
    /// <item><c>candidate % 8 == 0</c> — <c>CanvasItem</c>'s prefix through <c>window</c> is exactly
    /// 0x80 bytes and <c>sizeof(Node)</c> is 8-aligned, so <c>visible</c> is always congruent to 0
    /// mod 8. Every one of the four decoys that survived on real cells fails this.</item>
    /// <item>the qword at <c>candidate - 8</c> is <c>Window *window</c>: null, or a canonical heap
    /// pointer that is <em>not</em> made entirely of 0s and 1s. That last clause is what separates
    /// <c>visible</c> from <c>notify_local_transform</c>, the one other boolean at a multiple of
    /// eight, whose predecessor qword is eight bools.</item>
    /// </list>
    /// <para>
    /// <b>Everything else was removed after it emptied the candidate list on all 24 cell-runs.</b>
    /// The zero-padding equalities were the worst of them and were never sound: C++ does not zero
    /// struct padding and Godot does not memset its objects, so those bytes hold whatever the
    /// allocator last left there. The <c>clip_children_mode</c> enum test and the eleven-bool run
    /// were assertions about one build's exact field sequence, which is the kind of thing this
    /// calibrator exists to avoid assuming. Only <c>visible</c> and the byte after it are checked
    /// for being boolean, because both are certainly bools in every 4.x.
    /// </para>
    /// </remarks>
    public static bool? Examine(MemoryWindow window, int candidate, out string reason)
    {
        ArgumentNullException.ThrowIfNull(window);

        reason = string.Empty;

        if (candidate < 16)
        {
            reason = "below the start of the object";
            return false;
        }

        if (candidate % 8 != 0)
        {
            reason = "not 8-aligned; CanvasItem::visible always is";
            return false;
        }

        if (!window.IsReadable(candidate - 8, 10))
        {
            return null;
        }

        if (!window.TryPointer(candidate - 8, out ulong owner))
        {
            return null;
        }

        if (owner != 0)
        {
            bool allBytesBoolean = true;
            for (int i = 0; i < 8; i++)
            {
                if (((owner >> (i * 8)) & 0xFF) > 1)
                {
                    allBytesBoolean = false;
                    break;
                }
            }

            if (allBytesBoolean)
            {
                reason = $"the qword behind it (0x{owner:x}) is a run of bools, not a Window* — "
                       + "this is notify_local_transform";
                return false;
            }

            if (owner < 0x10000 || owner >= 1UL << 48 || owner % 8 != 0)
            {
                reason = $"the qword behind it (0x{owner:x}) is neither null nor a canonical Window*";
                return false;
            }
        }

        if (!Boolean(window, candidate) || !Boolean(window, candidate + 1))
        {
            reason = "visible / parent_visible_in_tree are not boolean-valued here";
            return false;
        }

        return true;
    }

    private static bool Boolean(MemoryWindow window, int offset)
        => window.TryByte(offset, out byte value) && value <= 1;

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) <= Epsilon;

    private static string Key((double First, double Second) expectation)
        => System.FormattableString.Invariant($"{expectation.First}x{expectation.Second}");

    private static bool Plausible(double value) => IsPlausibleReading(value);

    private static OffsetCandidates IntersectPerSample(
        string description,
        IEnumerable<(NodeSample Sample, (double, double) Expectation, int[] Candidates)> perSample)
    {
        List<(NodeSample Sample, (double, double) Expectation, int[] Candidates)> materialised = [.. perSample];
        if (materialised.Count == 0)
        {
            return new OffsetCandidates(description, [], CalibrationTechnique.Semantic, [], [], false);
        }

        IEnumerable<int> surviving = materialised[0].Candidates;
        foreach ((_, _, int[] candidates) in materialised.Skip(1))
        {
            surviving = surviving.Intersect(candidates);
        }

        return new OffsetCandidates(
            description,
            surviving,
            CalibrationTechnique.Semantic,
            materialised.Select(s => s.Sample.Address),
            materialised.Select(s => Key(s.Expectation)),
            materialised.All(s => s.Sample.Window.Complete));
    }

    private static OffsetCandidates BestSupported(
        string description,
        IReadOnlyList<(NodeSample Sample, (double, double) Expectation, int[] Candidates)> perSample,
        bool requireVariation,
        out IReadOnlyList<ulong> dissenting)
    {
        dissenting = [];
        if (perSample.Count == 0)
        {
            return new OffsetCandidates(description, [], CalibrationTechnique.Semantic, [], [], false);
        }

        Dictionary<int, List<int>> supporters = [];
        for (int i = 0; i < perSample.Count; i++)
        {
            foreach (int candidate in perSample[i].Candidates)
            {
                if (!supporters.TryGetValue(candidate, out List<int>? list))
                {
                    list = [];
                    supporters[candidate] = list;
                }

                list.Add(i);
            }
        }

        if (supporters.Count == 0)
        {
            return new OffsetCandidates(description, [], CalibrationTechnique.Semantic, [], [], perSample.All(s => s.Sample.Window.Complete));
        }

        int best = supporters.Values.Max(v => v.Count);
        List<int> winners = [.. supporters.Where(kv => kv.Value.Count == best).Select(kv => kv.Key).Order()];

        // A field that never varies cannot be told apart from a constant that happens to equal the
        // same default; only ever used as a tie-break, never to discard a lone candidate.
        if (requireVariation && winners.Count > 1)
        {
            List<int> varying = [.. winners.Where(w => supporters[w].Count < perSample.Count)];
            if (varying.Count > 0)
            {
                winners = varying;
            }
        }

        if (winners.Count != 1)
        {
            return new OffsetCandidates(
                description,
                winners,
                CalibrationTechnique.Semantic,
                perSample.Select(s => s.Sample.Address),
                perSample.Select(s => Key(s.Expectation)),
                perSample.All(s => s.Sample.Window.Complete));
        }

        List<int> supportingSamples = supporters[winners[0]];
        HashSet<int> supporting = [.. supportingSamples];
        dissenting = [.. Enumerable.Range(0, perSample.Count).Where(i => !supporting.Contains(i)).Select(i => perSample[i].Sample.Address)];

        List<string> expectations = [.. supportingSamples.Select(i => Key(perSample[i].Expectation))];

        // Where every supporting sample carried the same expected value, the discriminating evidence
        // is the set of samples that did NOT match: "equal to the default here, different there" is
        // two distinct expectations, and without the second half this would be indistinguishable
        // from any other constant holding the same value.
        if (expectations.Distinct().Count() == 1 && dissenting.Count > 0)
        {
            expectations.Add("differs-from-default");
        }

        return new OffsetCandidates(
            description,
            winners,
            CalibrationTechnique.Semantic,
            supportingSamples.Select(i => perSample[i].Sample.Address),
            expectations,
            perSample.All(s => s.Sample.Window.Complete));
    }
}
