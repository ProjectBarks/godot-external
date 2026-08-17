using Godot.External.Calibrator.Protocol;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>Contract keys, exactly as <c>lib/checks.mjs</c> and <c>profiles.json</c> spell them.</summary>
public static class OffsetKeys
{
    public const string NodeParent = "node.parent";
    public const string NodeChildListHead = "node.childListHead";
    public const string NodeScriptInstance = "node.scriptInstance";
    public const string NodeName = "node.name";

    /// <summary><c>Object::_class_name_ptr</c>. Not in any shipped profile; the harness ignores it.</summary>
    public const string ObjectClassName = "object.className";
    public const string CanvasItemVisible = "canvasItem.visible";
    public const string ControlOffset = "control.offset";
    public const string ControlScale = "control.scale";
    public const string ControlPosition = "control.position";
    public const string ControlSize = "control.size";
    public const string LabelText = "label.text";
    public const string RichTextLabelText = "richTextLabel.text";
    public const string ChildListNext = "childList.next";
    public const string ChildListNode = "childList.node";
    public const string ScriptInstanceOwner = "scriptInstance.ownerBackref";
    public const string ScriptInstanceGcHandle = "scriptInstance.gcHandle";
}

/// <summary>
/// One calibration run against one live target: ground truth in, derived offsets and node readings
/// out.
/// </summary>
/// <remarks>
/// <para>
/// The order is not arbitrary. Structural offsets come first because they need no numbers and
/// produce the node set every later pass samples across; <c>control.size</c> comes next because it
/// is the only field the harness states outright; <c>control.offset</c> falls out of the sizes as a
/// difference; and <c>control.position</c> and <c>control.scale</c> are anchored on those. Each step
/// is allowed to fail on its own and leave the rest running — a missing offset is reported missing.
/// </para>
/// <para>
/// Nothing here reads <c>GodotAbiProfiles</c>. The one place the shipped table is touched is
/// <see cref="CrossCheck"/>, which runs after every derivation is finished and whose output cannot
/// reach any offset, any candidate set or any node reading.
/// </para>
/// </remarks>
public sealed class CalibrationSession
{
    private const int GeometryScanBytes = 0xC00;
    private const int PointerScanBytes = 0xC00;

    private readonly IMemoryReader _reader;
    private readonly DriverRequest _request;
    private readonly IManagedProbe? _managed;
    private readonly Action? _refresh;
    private readonly GodotPrecisionWidth _precision;
    private readonly SemanticCalibrator _semantic;
    private readonly StructuralCalibrator _structural;

    private readonly Dictionary<string, int> _offsets = [];
    private readonly Dictionary<string, string> _structuralEvidence = [];
    private readonly Dictionary<string, string> _stringEvidence = [];
    private readonly Dictionary<string, int> _samples = [];
    private readonly Dictionary<string, IReadOnlyList<string>> _candidates = [];
    private readonly List<string> _notes = [];

    /// <summary>
    /// Creates a session.
    /// </summary>
    /// <remarks>
    /// <paramref name="refresh"/> invalidates any caching between the reader and the target, so that
    /// a second reading is actually a second reading. Several derivations here separate a stable
    /// field from per-frame state by sampling twice — and every one of them is a no-op against a page
    /// cache that answers the repeat from the bytes it already holds.
    /// </remarks>
    public CalibrationSession(
        IMemoryReader reader,
        DriverRequest request,
        IManagedProbe? managed = null,
        Action? refresh = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(request);

        _reader = reader;
        _request = request;
        _managed = managed;
        _refresh = refresh;
        _precision = GodotPrecisionWidth.For(request.IsDoublePrecision);
        _semantic = new SemanticCalibrator(_precision);
        _structural = new StructuralCalibrator(reader);
    }

    /// <summary>Runs the whole calibration against the node at <paramref name="rootPointer"/>.</summary>
    public DriverResult Run(ulong rootPointer)
    {
        _root = rootPointer;
        string rootName = _request.WalkRootName ?? string.Empty;
        int expectedNodes = _request.NodeCount ?? 0;

        if (rootName.Length == 0 || expectedNodes <= 0)
        {
            _notes.Add("request carried no walk root name or node count; nothing can be anchored without them.");
            return Build(0, [], null);
        }

        IReadOnlyList<CandidateLayout> layouts = NodeLayoutSolver.Solve(
            _reader, rootPointer, rootName, _request.KnownRootChildNames(), _request.Names, expectedNodes);

        if (layouts.Count == 0)
        {
            _notes.Add(
                $"no node layout reproduced the authored scene from root 0x{rootPointer:x}: no combination of "
                + "(name, child-list head, link next, link payload) reached "
                + $"{expectedNodes} nodes carrying the {_request.Names.Count} stated names.");
            return Build(0, [], null);
        }

        // Godot's List<Node *> stores `first` and `last` adjacently and links its elements both ways,
        // so a scene is reachable twice: forward from `first`, and backward from `last` through the
        // `prev` field. Both reproduce the node SET, and they differ only in child ORDER — which the
        // harness checks. The choice is therefore made deliberately, on upstream declaration order:
        // `first` is declared before `last`, so the lower head offset is the forward list and its
        // chain is the authored order.
        CandidateLayout layout = layouts.OrderBy(l => l.ChildListHead).ThenBy(l => l.ChildLinkNext).First();

        if (layouts.Count > 1)
        {
            _notes.Add(
                $"{layouts.Count} node layouts each reproduced the authored scene: "
                + string.Join(", ", layouts.Select(l => $"head 0x{l.ChildListHead:x}/next 0x{l.ChildLinkNext:x}"))
                + $". Taking head 0x{layout.ChildListHead:x} — Godot's List<Node *> holds `first` then `last` and "
                + "links elements both ways, so the higher pair is the same list walked backwards from its tail. "
                + "The lower offset is `first`, whose chain gives the authored child order; the node set is "
                + "identical either way, so only the order and the reported offsets differ.");
        }
        WalkedScene scene = SceneWalker.Walk(_reader, rootPointer, layout, expectedNodes + 8);

        // node.parent is derived from the child lists rather than used to build them, so the first
        // walk cannot have read it. Re-walk once it is known, and refuse to publish a parent that
        // does not then round-trip against the lists that produced it.
        DeriveStructural(scene, layout);
        if (_offsets.TryGetValue(OffsetKeys.NodeParent, out int parentOffset))
        {
            layout = layout with { ParentOffset = parentOffset };
            WalkedScene reWalked = SceneWalker.Walk(_reader, rootPointer, layout, expectedNodes + 8);
            if (SceneWalker.Reproduces(reWalked, expectedNodes, _request.Names))
            {
                scene = reWalked;
            }
            else
            {
                _offsets.Remove(OffsetKeys.NodeParent);
                _notes.Add("node.parent: the derived offset did not round-trip against the child lists on a "
                         + "second walk, so it is withdrawn rather than reported.");
            }
        }
        Dictionary<ulong, MemoryWindow> windows = ReadWindows(scene);
        Dictionary<string, ulong> byPath = MapPaths(scene, rootName);
        DeriveSemantic(scene, windows, byPath);
        EnforceStructuralCeiling();
        DeriveStrings(scene, layout, windows);

        ManagedBridgeResult? bridge = DeriveManagedBridge(rootPointer);
        return Build(scene.Nodes.Count, BuildNodes(scene, windows), bridge);
    }

    // -- (a) structural -------------------------------------------------------

    private void DeriveStructural(WalkedScene scene, CandidateLayout layout)
    {
        Record(OffsetKeys.ChildListNext, layout.ChildLinkNext);
        Record(OffsetKeys.ChildListNode, layout.ChildLinkPayload);

        // Recorded here rather than with the other string offsets, because the semantic pass needs it
        // as the lower half of the inheritance bracket on canvasItem.visible. Its evidence is still
        // written where the strings are reported.
        Record(OffsetKeys.NodeName, layout.NameOffset);

        List<(ulong Child, ulong Parent)> pairs = [];
        foreach (WalkedNode node in scene.Nodes)
        {
            foreach (ulong child in node.Children)
            {
                pairs.Add((child, node.Address));
            }
        }

        OffsetCandidates parent = _structural.DeriveParent(pairs, PointerScanBytes);
        Publish(OffsetKeys.NodeParent, parent, _structuralEvidence,
            $"the only slot equal to the node's own parent, across all {pairs.Count} parent/child pairs");

        List<OffsetCandidates> heads = [];
        foreach (WalkedNode node in scene.Nodes.Where(n => n.Children.Count > 0))
        {
            heads.Add(_structural.DeriveChildListHead(node.Address, node.Children[0], layout.ChildLinkPayload, PointerScanBytes));
        }

        OffsetCandidates head = OffsetCandidates.Intersect(
            "node.childListHead (only slot p where *(p + payload) is a known child)", heads);
        Publish(OffsetKeys.NodeChildListHead, head, _structuralEvidence,
            $"the only slot p where *(p + 0x{layout.ChildLinkPayload:x}) is a known child, "
            + $"intersected across {heads.Count} parents");

        DeriveScriptInstanceChain(scene);
    }

    /// <summary>
    /// <c>node.scriptInstance</c> and the <c>ScriptInstance</c> owner back-reference, together.
    /// </summary>
    /// <remarks>
    /// How well this resolves depends on how many nodes carry a script. A debug-template scene with
    /// several scripted nodes intersects down to one answer; a release scene where only the walk root
    /// is scripted often leaves two or three <c>(slot, backref)</c> pairs that are each unique on
    /// that single sample and indistinguishable from each other by pointer identity alone. Rather
    /// than report nothing, the survivors are handed to the managed bridge, where they are separated
    /// by the one test that actually distinguishes a <c>ScriptInstance</c> from a lookalike: only the
    /// real one's GCHandle dereferences to a managed object of the expected type. Nothing is
    /// published until something has settled it.
    /// </remarks>
    private void DeriveScriptInstanceChain(WalkedScene scene)
    {
        List<ulong> nodes = [.. scene.Nodes.Select(n => n.Address)];
        List<(int Slot, int Backref, OffsetCandidates Candidates)> first = SolveScriptInstance(nodes);

        // Sampled twice. A scene where only the root is scripted gives one sample, and one sample
        // admits several (slot, backref) pairs that fit by accident — a transient allocation that
        // happens to point back at the node. Those do not survive to a second reading; a
        // ScriptInstance does. This is what made the derived owner-backref differ between runs
        // against an unchanging target.
        _refresh?.Invoke();
        List<(int Slot, int Backref, OffsetCandidates Candidates)> second = SolveScriptInstance(nodes);

        // The second reading may only ELIMINATE, and only on positive evidence. If it resolved a
        // different slot for the same back-reference, the first reading was transient; if it resolved
        // nothing at all, it failed and has said nothing — absence of evidence is not evidence, and
        // treating it as such is what withheld this offset on half the GDScript runs against
        // unchanged binaries.
        List<(int Slot, int Backref, OffsetCandidates Candidates)> solved;
        bool ownerConfirmed = true;
        if (second.Count == 0)
        {
            // A reading that resolved nothing has failed, not contradicted — so the slot survives.
            // The BACK-REFERENCE does not: it published 0x30, 0x10 and 0x8 on three runs of one
            // unchanged binary, so at most one was right and at least two were wrong-and-published.
            // Unlike the slot it has no ceiling to catch it and no managed bridge to corroborate it
            // on a GDScript cell, so it is held to positive confirmation instead.
            solved = first;
            ownerConfirmed = false;
            _notes.Add("node.scriptInstance: the confirming reading resolved nothing at all, so it is treated as a "
                     + "failed read rather than as a contradiction — but scriptInstance.ownerBackref is withheld, "
                     + "since nothing then corroborates which slot inside the ScriptInstance is the owner.");
        }
        else
        {
            Dictionary<int, int> byBackref = second.ToDictionary(s => s.Backref, s => s.Slot);
            solved = [.. first.Where(s => byBackref.TryGetValue(s.Backref, out int slot) && slot == s.Slot)];

            if (first.Count != solved.Count)
            {
                _notes.Add($"node.scriptInstance: {first.Count - solved.Count} candidate chain(s) were contradicted "
                         + "by a second reading and discarded as transient.");
            }
        }

        if (solved.Count == 0)
        {
            _notes.Add("node.scriptInstance: no slot held a pointer whose back-reference was the node itself; "
                     + "either no node in this scene carries a script instance, or the chain differs from §4.6's.");
            return;
        }

        // Corroboration first: a pair confirmed by several scripted nodes beats one seen on a single
        // node, and that is ordinary intersection rather than a tie-break.
        int best = solved.Max(s => s.Candidates.SampleCount);
        List<(int Slot, int Backref, OffsetCandidates Candidates)> strongest = [.. solved.Where(s => s.Candidates.SampleCount == best)];

        if (strongest.Count == 1)
        {
            PublishScriptInstance(strongest[0].Slot, strongest[0].Backref, strongest[0].Candidates, ownerConfirmed);
            return;
        }

        _scriptInstanceCandidates = [.. strongest.Select(s => (s.Slot, s.Backref))];
        _notes.Add(
            $"node.scriptInstance: {strongest.Count} (slot, owner-backref) pairs each fit, all corroborated by the "
            + $"same {best} scripted node(s) — too few to separate them by pointer identity. Deferred to the managed "
            + "bridge, which can settle it by following each candidate's GCHandle.");
    }

    private List<(int Slot, int Backref, OffsetCandidates Candidates)> SolveScriptInstance(IReadOnlyList<ulong> nodes)
    {
        List<(int Slot, int Backref, OffsetCandidates Candidates)> solved = [];
        foreach (int backref in StructuralCalibrator.LinkOffsetCandidates)
        {
            OffsetCandidates candidates = _structural.DeriveScriptInstance(nodes, backref, PointerScanBytes);
            if (candidates.TryGetOffset(out int slot))
            {
                solved.Add((slot, backref, candidates));
            }
        }

        return solved;
    }

    private void PublishScriptInstance(int slot, int backref, OffsetCandidates candidates, bool ownerConfirmed)
    {
        Publish(OffsetKeys.NodeScriptInstance, candidates, _structuralEvidence,
            $"the only slot holding a pointer whose +0x{backref:x} is the node itself — §4.6's owner "
            + "back-reference used as the derivation rather than as an afterthought");

        if (_offsets.ContainsKey(OffsetKeys.NodeScriptInstance))
        {
            _offsets[OffsetKeys.NodeScriptInstance] = slot;
            _scriptInstanceClass = ReadScriptInstanceClass(slot);

            // No per-binding rationalisation here any more. There WAS one, explaining a derived 0x30
            // as an expected GDScript-versus-.NET difference — on a cell whose other two runs gave
            // 0x8. A note that explains away a value the same binary contradicts is worse than none:
            // it dresses a flapping derivation as a finding.
            if (ownerConfirmed)
            {
                Record(OffsetKeys.ScriptInstanceOwner, backref);
            }
            else
            {
                _notes.Add($"scriptInstance.ownerBackref: 0x{backref:x} was not confirmed by a second reading, so "
                         + "it is withheld.");
            }
        }
    }

    private IReadOnlyList<(int Slot, int Backref)> _scriptInstanceCandidates = [];
    private string? _scriptInstanceClass;
    private ulong _root;

    /// <summary>
    /// Names the C++ class implementing the walk root's <c>ScriptInstance</c>, off its own vtable.
    /// </summary>
    /// <remarks>
    /// The same RTTI route the node classes come from, pointed at a different object. It matters
    /// because <c>scriptInstance.ownerBackref</c> belongs to this class rather than to the engine
    /// object — so it is the one derived offset whose expected value cannot be read off the build
    /// axes alone.
    /// </remarks>
    private string? ReadScriptInstanceClass(int slot)
    {
        // Retried, and the failure reported. This came back CSharpInstance, then null, then null on
        // three passes over one unchanged binary — harmless only while no 4.3 profile exists, because
        // the moment one does, ownerBackref silently becomes "not compared" on the runs where it went
        // missing. A comparison that cannot fail because its input vanished is not a passing
        // comparison.
        if (_root == 0)
        {
            _notes.Add("scriptInstance.class: no walk root, so the ScriptInstance could not be reached.");
            return null;
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (_reader.TryReadPointer(_root + (ulong)slot, out ulong scriptInstance)
                && scriptInstance != 0
                && _reader.TryReadPointer(scriptInstance, out ulong vtable)
                && vtable != 0
                && ClassNameCalibrator.TryReadClassName(_reader, vtable, out string name, out _))
            {
                return name;
            }
        }

        _notes.Add($"scriptInstance.class: the ScriptInstance at node + 0x{slot:x} did not resolve a class name "
                 + "through its vtable after three attempts, so scriptInstance.ownerBackref cannot be scoped to "
                 + "an implementing class and any cross-check of it will report 'not compared' rather than pass.");
        return null;
    }


    // -- (b) semantic ---------------------------------------------------------

    private Dictionary<ulong, MemoryWindow> ReadWindows(WalkedScene scene)
    {
        Dictionary<ulong, MemoryWindow> windows = [];
        foreach (WalkedNode node in scene.Nodes)
        {
            windows[node.Address] = MemoryWindow.Read(_reader, node.Address, GeometryScanBytes);
        }

        return windows;
    }

    private void DeriveSemantic(WalkedScene scene, Dictionary<ulong, MemoryWindow> windows, Dictionary<string, ulong> byPath)
    {
        List<(NodeSample Sample, double Width, double Height)> sizeSamples = [];
        foreach (SizeAnchor anchor in _request.Sizes)
        {
            if (byPath.TryGetValue(anchor.Path, out ulong address) && windows.TryGetValue(address, out MemoryWindow? window))
            {
                sizeSamples.Add((new NodeSample(address, window), anchor.Width, anchor.Height));
            }
        }

        if (sizeSamples.Count < 2)
        {
            _notes.Add($"only {sizeSamples.Count} of {_request.Sizes.Count} size anchors resolved to a walked node; "
                     + "§12.5 needs at least two with different sizes, so the geometry chain stops here.");
            return;
        }

        OffsetCandidates size = _semantic.DeriveSize(sizeSamples);
        Publish(OffsetKeys.ControlSize, size, null, null);

        OffsetCandidates quad = _semantic.DeriveOffsetQuad(sizeSamples, _offsets.GetValueOrDefault(OffsetKeys.ControlSize, -1));
        Publish(OffsetKeys.ControlOffset, quad, null, null);

        if (!_offsets.TryGetValue(OffsetKeys.ControlOffset, out int offsetBase))
        {
            _notes.Add("control.offset undetermined, so control.position and control.scale — which are anchored on "
                     + "it rather than on anything the harness stated — are not attempted.");
        }
        else
        {
            DerivePosition(scene, windows, offsetBase);
        }

        DeriveVisible(scene, windows, byPath);
    }

    private void DerivePosition(WalkedScene scene, Dictionary<ulong, MemoryWindow> windows, int offsetBase)
    {
        List<(NodeSample Sample, double X, double Y)> samples = [];
        Span<double> quad = stackalloc double[4];
        foreach (WalkedNode node in scene.Nodes)
        {
            if (windows.TryGetValue(node.Address, out MemoryWindow? window)
                && window.TryReals(offsetBase, _precision, 4, quad))
            {
                samples.Add((new NodeSample(node.Address, window), quad[0], quad[1]));
            }
        }

        OffsetCandidates position = _semantic.DerivePosition(samples, offsetBase, out IReadOnlyList<ulong> dissenting);
        Publish(OffsetKeys.ControlPosition, position, null, null);

        if (dissenting.Count > 0 && _offsets.ContainsKey(OffsetKeys.ControlPosition))
        {
            _notes.Add(
                $"control.position: {dissenting.Count} of {samples.Count} node(s) read a position that is not "
                + "offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * "
                + "parent_size. Those nodes are the reason this derivation counts support instead of demanding "
                + "unanimity: " + string.Join(", ", dissenting.Select(Wire.Pointer)));
        }

        if (!_offsets.TryGetValue(OffsetKeys.ControlPosition, out int positionBase))
        {
            return;
        }

        OffsetCandidates scale = _semantic.DeriveScale(
            [.. samples.Select(s => s.Sample)], 1.0, 1.0, offsetBase, positionBase);
        Publish(OffsetKeys.ControlScale, scale, null, null);

        if (_offsets.ContainsKey(OffsetKeys.ControlScale))
        {
            _notes.Add(
                "control.scale is the weakest derivation reported here. The harness states no scales, so the "
                + "known value is upstream's declared default Vector2(1,1); it is separated from "
                + "CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting "
                + "the scan to the region between the derived control.offset and control.position — a base class "
                + "is laid out before its derived class — and by requiring the field to actually vary.");
        }
    }

    private void DeriveVisible(WalkedScene scene, Dictionary<ulong, MemoryWindow> windows, Dictionary<string, ulong> byPath)
    {
        VisibilityAnchor? twins = _request.Visibility;
        if (twins is null
            || !byPath.TryGetValue(twins.VisiblePath, out ulong visibleAddress)
            || !byPath.TryGetValue(twins.HiddenPath, out ulong hiddenAddress)
            || !windows.TryGetValue(visibleAddress, out MemoryWindow? visibleWindow)
            || !windows.TryGetValue(hiddenAddress, out MemoryWindow? hiddenWindow))
        {
            _notes.Add("canvasItem.visible: the visible/hidden twins did not resolve to walked nodes.");
            return;
        }

        // Only nodes that read as Controls are sampled. The CanvasItem bracket describes a
        // CanvasItem, and a plain Node has neither those fields nor anything meaningful at that
        // offset — asking it to satisfy the layout would reject the true answer on the strength of
        // whatever happens to sit past the end of a smaller object.
        List<NodeSample> all = [.. scene.Nodes
            .Where(n => windows.ContainsKey(n.Address) && ReadsAsControl(windows[n.Address]))
            .Select(n => new NodeSample(n.Address, windows[n.Address]))];

        // CanvasItem is Control's base class, so on any single-inheritance ABI its fields precede
        // every Control::Data field. Where control.offset is known this bounds the search honestly;
        // where it is not, the whole window is searched and the result is more likely ambiguous.
        int bound = _offsets.TryGetValue(OffsetKeys.ControlOffset, out int controlBase) ? controlBase : GeometryScanBytes;

        // The other half of the bracket: every Node member is below every CanvasItem member.
        int nodeLevel = 0;
        foreach (string key in new[]
        {
            OffsetKeys.NodeParent, OffsetKeys.NodeName,
            OffsetKeys.NodeChildListHead, OffsetKeys.NodeScriptInstance,
        })
        {
            if (_offsets.TryGetValue(key, out int offset) && offset > nodeLevel)
            {
                nodeLevel = offset;
            }
        }

        List<string> diagnostics = [];
        OffsetCandidates visible = _semantic.DeriveVisible(
            new NodeSample(visibleAddress, visibleWindow),
            new NodeSample(hiddenAddress, hiddenWindow),
            all,
            bound,
            diagnostics,
            nodeLevel);

        Publish(OffsetKeys.CanvasItemVisible, visible, null, null);

        if (!_offsets.ContainsKey(OffsetKeys.CanvasItemVisible) && diagnostics.Count > 0)
        {
            _notes.Add("canvasItem.visible: every nominated byte was eliminated. Which rule did it, per "
                     + "candidate — " + string.Join("; ", diagnostics.Take(12)));
        }
    }

    // -- (c) strings ----------------------------------------------------------

    private void DeriveStrings(WalkedScene scene, CandidateLayout layout, Dictionary<ulong, MemoryWindow> windows)
    {
        Record(OffsetKeys.NodeName, layout.NameOffset);
        _stringEvidence[OffsetKeys.NodeName] =
            $"StringName at node + 0x{layout.NameOffset:x} -> _Data + 0x{layout.StringNameDataToBuffer:x} -> "
            + $"CowData<char32_t>; accepted only because all {scene.Nodes.Count} decoded names are exactly the "
            + "set the harness listed";
        _samples[OffsetKeys.NodeName] = scene.Nodes.Count;

        TextFieldSet fields = TextCalibrator.Discover(_reader, [.. scene.Nodes.Select(n => n.Address)]);

        // Class layout, and NOTHING ELSE.
        //
        // There was a fallback here that took "strings unique to one node" when the bracket found
        // nothing. It published junk: 0x110 and 0x118 on real cells, both BELOW node.parent, one of
        // them reading "Color" where the authored text was "ρich ✦ テキスト 𝄞 RTL". That was the first
        // time in the whole series this calibrator reported a wrong value instead of withholding,
        // and absent-never-wrong is the property that makes every other number here worth anything.
        // A fallback fires exactly when the bracket failed — which is precisely when the least is
        // known, and the worst possible moment to lower the bar. It is gone.
        // The floor is only meaningful if something is under it. With no Control member derived it
        // collapses to zero and the whole Node/Object header becomes admissible again — which is
        // where "Color" and "res://Probe.gd" came from. A cell that could not place a single Control
        // field has not earned the right to name a Label field either.
        if (!_offsets.ContainsKey(OffsetKeys.ControlSize)
            && !_offsets.ContainsKey(OffsetKeys.ControlOffset)
            && !_offsets.ContainsKey(OffsetKeys.CanvasItemVisible))
        {
            _notes.Add("strings: no CanvasItem or Control member was derived, so there is no floor under which "
                     + "to reject Node-header strings. Text is withheld unconditionally rather than scanned "
                     + "from offset zero.");
            return;
        }

        int floor = MemberFloor();
        int controlNodes = scene.Nodes.Count(n => windows.TryGetValue(n.Address, out MemoryWindow? w) && ReadsAsControl(w));
        IReadOnlyList<TextField> labelPool = NotAControlMember(AboveFloor(fields.Label, floor), controlNodes);
        IReadOnlyList<TextField> richPool = NotAControlMember(AboveFloor(fields.RichTextLabel, floor), controlNodes);

        // Everything above describes a field's SHAPE, and shape cannot answer "is this node a
        // Label?". Without that answered, a class was whatever set of nodes an offset happened to
        // decode on — so a plain Node holding a font path, and a Control holding a scene path, joined
        // a text class and were handed invented text. The engine knows the answer; read it.
        _classes = DeriveClassNames(scene, windows, layout);
        labelPool = OfClass(OffsetKeys.LabelText, labelPool, "Label");
        richPool = OfClass(OffsetKeys.RichTextLabelText, richPool, "RichTextLabel");

        ReportBracketRejections(fields, labelPool.Count == 0, richPool.Count == 0);

        if (labelPool.Count == 0)
        {
            _notes.Add("label.text: no offset above the derived Node/Control members matched Godot's Label "
                     + "layout (String text with String xl_text after it, alignment enums behind, autowrap "
                     + "ahead). Withheld.");
        }

        if (richPool.Count == 0)
        {
            _notes.Add("richTextLabel.text: no offset above the derived Node/Control members matched Godot's "
                     + "RichTextLabel layout (use_bbcode behind, bools ahead, and NO xl_text). Withheld.");
        }

        // The two classes are disjoint — a Label is not a RichTextLabel — so whatever the Label class
        // claims is off the table for the next pass. Without that, an unrelated one-off string on a
        // Label is the only member of its own single-node "class" and gets published as
        // richTextLabel.text, taking the real one's text down with it.
        HashSet<ulong> claimed = PublishClassText(OffsetKeys.LabelText, labelPool, []);
        PublishClassText(OffsetKeys.RichTextLabelText, richPool, claimed);
    }

    /// <summary>
    /// Withdraws any <c>Object</c>/<c>Node</c>-level offset that landed above a <c>CanvasItem</c> or
    /// <c>Control</c> member.
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="MemberFloor"/>, and the same single-inheritance argument in the
    /// opposite direction: a base class is laid out first, so a <c>Node</c> member cannot sit above
    /// any member of a class derived from it. One run published
    /// <c>node.scriptInstance = 0x968</c> — above <c>control.size</c>, and therefore structurally
    /// impossible — because the floor reasoning had only ever been applied to text. Pointer identity
    /// alone cannot catch this: the slot really did hold a pointer whose back-reference was the node.
    /// </remarks>
    private void EnforceStructuralCeiling()
    {
        int ceiling = int.MaxValue;
        foreach (string key in new[]
        {
            OffsetKeys.CanvasItemVisible, OffsetKeys.ControlOffset,
            OffsetKeys.ControlScale, OffsetKeys.ControlPosition, OffsetKeys.ControlSize,
        })
        {
            if (_offsets.TryGetValue(key, out int offset) && offset < ceiling)
            {
                ceiling = offset;
            }
        }

        if (ceiling == int.MaxValue)
        {
            return;
        }

        // Object's own members are below NODE's, not merely below CanvasItem's. Using the lowest
        // CanvasItem/Control member left a gap hundreds of bytes wide, and node.scriptInstance was
        // published at 0x3f8 through it — below canvasItem.visible at 0x418, so the loose ceiling
        // waved it past, on a binary that gave 0x68 on the other two runs. Only bridge.managed
        // caught it, and that check does not exist on a GDScript cell.
        int objectCeiling = ceiling;
        foreach (string key in new[] { OffsetKeys.NodeParent, OffsetKeys.NodeName, OffsetKeys.NodeChildListHead })
        {
            if (_offsets.TryGetValue(key, out int offset) && offset < objectCeiling)
            {
                objectCeiling = offset;
            }
        }

        foreach ((string key, int limit) in new[]
        {
            (OffsetKeys.NodeParent, ceiling),
            (OffsetKeys.NodeName, ceiling),
            (OffsetKeys.NodeChildListHead, ceiling),
            (OffsetKeys.NodeScriptInstance, objectCeiling),
        })
        {
            if (_offsets.TryGetValue(key, out int offset) && offset >= limit)
            {
                _offsets.Remove(key);
                _structuralEvidence.Remove(key);
                _notes.Add($"{key}: derived {Wire.Offset(offset)}, which is at or above the lowest member of a "
                         + $"class derived from the one it belongs to ({Wire.Offset(limit)}). A base class is laid "
                         + "out before the classes derived from it, so that is structurally impossible and the "
                         + "offset is withdrawn rather than reported.");
            }
        }
    }

    /// <summary>
    /// The highest offset already derived for a <c>Node</c>, <c>CanvasItem</c> or <c>Control</c>
    /// member. Nothing belonging to <c>Label</c> or <c>RichTextLabel</c> can be at or below it.
    /// </summary>
    /// <remarks>
    /// Single inheritance settles this outright: a derived class's own members begin at or after
    /// <c>sizeof(base)</c>, so <c>Label::text</c> is above <em>every</em> <c>Control</c> member,
    /// which is above every <c>CanvasItem</c> member, which is above every <c>Node</c> member. The
    /// junk that reached a published result — <c>0x110</c> and <c>0x118</c>, sitting inside the
    /// Node/Object header below <c>node.parent</c> — was structurally impossible, and no amount of
    /// CowData validation could have caught it, because those really were valid CowData buffers.
    /// A <c>StringName</c>'s character buffer is a genuine Godot String; it is simply not this one.
    /// </remarks>
    private int MemberFloor()
    {
        int floor = 0;
        foreach (string key in new[]
        {
            OffsetKeys.NodeParent, OffsetKeys.NodeName, OffsetKeys.NodeChildListHead,
            OffsetKeys.NodeScriptInstance, OffsetKeys.CanvasItemVisible, OffsetKeys.ControlOffset,
            OffsetKeys.ControlScale, OffsetKeys.ControlPosition, OffsetKeys.ControlSize,
        })
        {
            if (_offsets.TryGetValue(key, out int offset) && offset > floor)
            {
                floor = offset;
            }
        }

        return floor;
    }

    /// <summary>
    /// Says which bracket clause rejected the offsets that did decode, when a class found none.
    /// </summary>
    private void ReportBracketRejections(TextFieldSet fields, bool labelEmpty, bool richEmpty)
    {
        foreach ((string what, string why) in fields.Rejections)
        {
            bool wanted = (labelEmpty && what.StartsWith("label", StringComparison.Ordinal))
                       || (richEmpty && what.StartsWith("richTextLabel", StringComparison.Ordinal));
            if (wanted)
            {
                _notes.Add($"bracket rejected {what}: {why}");
            }
        }
    }

    private static IReadOnlyList<TextField> AboveFloor(IReadOnlyList<TextField> fields, int floor)
        => [.. fields.Where(f => f.Offset > floor)];

    private ClassNameMap? _classes;

    private ClassNameMap? DeriveClassNames(WalkedScene scene, Dictionary<ulong, MemoryWindow> windows, CandidateLayout layout)
    {
        _ = windows;
        _ = layout;

        ClassNameMap? map = ClassNameCalibrator.Derive(
            _reader,
            [.. scene.Nodes.Select(n => n.Address)],
            out string diagnosis);

        if (map is null)
        {
            _notes.Add($"object.class: {diagnosis}. All text is withheld on this build — nothing else in a Godot "
                     + "node carries per-instance class identity (get_class is a virtual returning a literal, and "
                     + "_get_class_namev returns a per-class static), and geometry cannot tell a Label from any "
                     + "other Control.");
            return null;
        }

        if (!ClassNameCalibrator.Corroborated(map.Names, scene.Nodes.ToDictionary(n => n.Address, n => n.Name)))
        {
            _notes.Add("object.class: the walk contains a node Godot named after its own class, and the vtable "
                     + "disagrees with it. Class identity is withheld rather than trusted.");
            return null;
        }

        _structuralEvidence["object.class"] =
            $"read from each node's vtable through Itanium RTTI (vptr at +0, offset-to-top 0, type_info at -8), "
            + $"which needs no calibration at all; {diagnosis}";

        return map;
    }

    /// <summary>
    /// Keeps only candidates whose node set is exactly the set of instances of
    /// <paramref name="className"/>.
    /// </summary>
    /// <remarks>
    /// Exactly, not "mostly". One stray node with a valid String at the same offset was all it took:
    /// an offset that decoded both Labels correctly <em>and</em> the walk root joined them into one
    /// "class" and the root was handed <c>"res://Probe.gd"</c>. Set size cannot catch that — three of
    /// fourteen Controls is not a majority — and neither can any check on the string, which was a
    /// perfectly valid one.
    /// </remarks>
    private IReadOnlyList<TextField> OfClass(string key, IReadOnlyList<TextField> fields, string className)
    {
        if (fields.Count == 0)
        {
            return fields;
        }

        if (_classes is null)
        {
            // No geometry fallback. It fired on exactly the cells where class identity was missing,
            // leaked 100% of the phantoms across two consecutive series, and never once caught the
            // case that matters — a real Control of the wrong class — because it structurally
            // cannot: the walk root IS a Control, and so is a Label. A fallback that fires when the
            // least is known is the same mistake as the text fallback retired two rounds ago.
            _notes.Add($"{key}: the engine's class names could not be derived on this build, so there is no way to "
                     + "tell a Label from any other Control. Text is withheld entirely rather than guessed at "
                     + "from geometry.");
            return [];
        }

        HashSet<ulong> instances = _classes.Instances(className);

        // The subset rule's safety is proportional to instance count, and at one it is ZERO: with a
        // singleton class, ANY String-shaped field on that one node is trivially a subset of it. That
        // is not a near miss, it is the rule not applying — and it is exactly how a wrong
        // richTextLabel.text got published while label.text, which has two instances, never did.
        // A lone candidate on a lone instance is the "last one standing" situation that has been
        // wrong repeatedly, so it is withheld until something independent can corroborate it.
        if (instances.Count == 1)
        {
            _notes.Add($"{key}: the scene contains exactly ONE node of class \"{className}\", so requiring the "
                     + "decode-set to lie inside the class-set constrains nothing at all — any string-shaped field "
                     + "on that node satisfies it. Withheld for want of a second, independent signal.");
            return [];
        }

        if (instances.Count == 0)
        {
            _notes.Add($"{key}: the walk contains no node of class \"{className}\", so there is no such field to "
                     + "derive here.");
            return [];
        }

        // SUBSET, not equality. The safety property is "nothing outside the class decodes here" —
        // a phantom is by definition a decode on a node the engine does not call this class, and
        // that is exactly what a subset test rejects. Equality buys no safety on top of that and
        // costs a great deal of coverage: a Label with empty text, or one whose CowData momentarily
        // fails a validator, simply does not decode, and demanding every instance decode at once
        // made correctness hostage to all of them being readable in the same instant. It discarded
        // the correct, shipped-profile offset on the reference cell for that reason.
        List<TextField> kept = [];
        foreach (TextField field in fields)
        {
            if (field.Values.Count > 0 && instances.IsSupersetOf(field.Values.Keys))
            {
                kept.Add(field);
            }
            else
            {
                IEnumerable<ulong> strangers = field.Values.Keys.Where(n => !instances.Contains(n));
                _notes.Add($"{key}: {Wire.Offset(field.Offset)} discarded — it also decodes on "
                         + $"{strangers.Count()} node(s) the engine does not call \"{className}\" "
                         + $"({string.Join(", ", strangers.Take(4).Select(Wire.Pointer))}).");
            }
        }

        return kept;
    }

    /// <summary>
    /// Drops candidates that yield a string on most of the scene's Controls.
    /// </summary>
    /// <remarks>
    /// The negative signal the text path was missing, and one no validity check can supply — the junk
    /// <em>is</em> a valid Godot String, just the wrong one. The floor leaves several hundred bytes of
    /// unmeasured <c>Control</c> internals admissible, and a <c>Control</c>-level String sits at the
    /// same offset on <em>every</em> node in the scene. A <c>Label::text</c> cannot: it exists only
    /// on Labels, which are a small minority of any real tree. So an offset that answers almost
    /// everywhere is a base-class member being read through a derived-class hypothesis.
    /// </remarks>
    private IReadOnlyList<TextField> NotAControlMember(IReadOnlyList<TextField> fields, int controlNodes)
    {
        if (controlNodes < 4)
        {
            return fields; // too few nodes for "most of them" to mean anything
        }

        List<TextField> kept = [];
        foreach (TextField field in fields)
        {
            if (field.Values.Count * 2 > controlNodes)
            {
                _notes.Add($"strings: {Wire.Offset(field.Offset)} yields a valid String on {field.Values.Count} of "
                         + $"{controlNodes} Controls — that is a Control member read through a Label hypothesis, "
                         + "not a Label field, and it is discarded.");
                continue;
            }

            kept.Add(field);
        }

        return kept;
    }

    /// <summary>
    /// Turns a pool of per-node string readings into one class-level offset, and reduces the pool to
    /// that offset so every node of the class is then read through the same fact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reporting per-node text without an offset behind it was the wrong shape of answer. It passes
    /// or fails one node at a time, so a lucky read scores and an unlucky one does not, and nothing
    /// is ever <em>established</em> — which is also why the profile's <c>label.text</c> and
    /// <c>richTextLabel.text</c> could never be cross-checked at all. An offset that yields a
    /// decodable string for every node of a class is a class fact, and it stands or falls as one.
    /// </para>
    /// <para>
    /// Candidates that survive here agree on every node's string, so the reading is not in question;
    /// only its address is, because Godot stores a Label's text twice (<c>String text</c> then
    /// <c>String xl_text</c>, the translated copy). That is settled by upstream declaration order —
    /// the same argument used for <c>List::first</c> and for the <c>Control::Data</c> scale region —
    /// and the alternatives stay visible in <c>candidates</c> rather than being quietly dropped.
    /// </para>
    /// </remarks>
    private HashSet<ulong> PublishClassText(string key, IReadOnlyList<TextField> pool, HashSet<ulong> claimed)
    {
        List<TextField> eligible = [.. pool.Where(f => !f.Values.Keys.Any(claimed.Contains))];
        if (eligible.Count == 0)
        {
            return [];
        }

        // A class is a node SET, so candidates are grouped by exactly which nodes carry them. Two
        // offsets that disagree about which nodes have text are not describing the same field, and
        // the class is the largest such group.
        List<IGrouping<string, TextField>> groups =
            [.. eligible.GroupBy(f => string.Join(",", f.Values.Keys.Order()))];

        int widest = groups.Max(g => g.First().Values.Count);
        List<IGrouping<string, TextField>> largest = [.. groups.Where(g => g.First().Values.Count == widest)];

        if (largest.Count > 1)
        {
            _notes.Add($"{key}: {largest.Count} equally large groups of string offsets cover DIFFERENT node sets, "
                     + "so which one is the class is undecided. No offset is reported AND no text is read "
                     + "through these candidates.");
            return [];
        }

        List<TextField> sameClass = [.. largest[0].OrderBy(f => f.Offset)];
        HashSet<ulong> nodes = [.. sameClass[0].Values.Keys];
        _candidates[key] = [.. sameClass.Select(f => Wire.Offset(f.Offset))];

        // Plurality, not unanimity. Requiring every candidate to concur let one junk offset veto
        // several that decoded the authored string exactly — the calibrator found the right answer
        // and refused to publish it. A reading corroborated by more candidates than any other is the
        // answer; the dissenters stay visible under `candidates` instead of suppressing it.
        Dictionary<ulong, string> agreed = [];
        foreach (ulong node in nodes)
        {
            List<IGrouping<string, string>> byValue =
                [.. sameClass.Select(f => f.Values[node]).GroupBy(v => v).OrderByDescending(g => g.Count())];

            if (byValue.Count > 1 && byValue[0].Count() == byValue[1].Count())
            {
                _notes.Add($"{key}: candidates split evenly on node {Wire.Pointer(node)}, with nothing to prefer "
                         + "between them, so no class offset is reported and NO text is read through these "
                         + "candidates — "
                         + string.Join(", ", sameClass.Select(f => $"{Wire.Offset(f.Offset)}=\"{f.Values[node]}\"")));
                return [];
            }

            agreed[node] = byValue[0].Key;
        }

        List<TextField> concurring = [.. sameClass.Where(f => nodes.All(n => f.Values[n] == agreed[n]))];
        TextField chosen = concurring[0];

        Record(key, chosen.Offset);
        _samples[key] = nodes.Count;
        _stringEvidence[key] =
            $"the offset yields a structurally valid CowData<char32_t> on all {nodes.Count} node(s) carrying this "
            + $"field, and {concurring.Count} of {sameClass.Count} candidate(s) with that exact node set agree on "
            + "every one of them"
            + (concurring.Count > 1
                ? $"; the lowest ({Wire.Offset(chosen.Offset)}) is taken because upstream declares String text "
                  + "before its translated copy, and the rest stay listed under candidates"
                : string.Empty);

        if (concurring.Count < sameClass.Count)
        {
            _notes.Add($"{key}: {sameClass.Count - concurring.Count} candidate(s) dissented from the reading the "
                     + "majority agreed on and were set aside rather than allowed to withhold it.");
        }

        return nodes;
    }


    // -- managed bridge -------------------------------------------------------

    private ManagedBridgeResult? DeriveManagedBridge(ulong rootPointer)
    {
        if (!_request.IsDotNetCell || _request.ManagedStatic is null)
        {
            return null;
        }

        if (_managed is null)
        {
            _notes.Add("bridge.managed: no managed probe was available for this run, so the CLR half of §4.6's "
                     + "chain was not followed.");
            return null;
        }

        List<(int Slot, int Backref)> chains = _offsets.TryGetValue(OffsetKeys.NodeScriptInstance, out int settledSlot)
            && _offsets.TryGetValue(OffsetKeys.ScriptInstanceOwner, out int settledBackref)
                ? [(settledSlot, settledBackref)]
                : [.. _scriptInstanceCandidates];

        if (chains.Count == 0)
        {
            _notes.Add("bridge.managed: the root node exposed no ScriptInstance, so there is no route to a managed "
                     + "object from the native side.");
            return null;
        }

        string[] wanted = ["ProbeAscii", "ProbeUnicode", "ProbeInt32", "ProbeInt64", "ProbeFloat", "ProbeBool"];

        foreach ((int slot, int backref) in chains)
        {
            if (!_reader.TryReadPointer(rootPointer + (ulong)slot, out ulong scriptInstance) || scriptInstance == 0)
            {
                continue;
            }

            _reader.TryReadPointer(scriptInstance + (ulong)backref, out ulong owner);

            foreach (int gcHandleOffset in StructuralCalibrator.LinkOffsetCandidates)
            {
                if (!_reader.TryReadPointer(scriptInstance + (ulong)gcHandleOffset, out ulong handleSlot)
                    || handleSlot == 0
                    || !_reader.TryReadPointer(handleSlot, out ulong managedObject)
                    || managedObject == 0
                    || !_managed.TryDescribe(managedObject, wanted, out ManagedObjectInfo info))
                {
                    continue;
                }

                string shortName = info.TypeName[(info.TypeName.LastIndexOf('.') + 1)..];
                if (!string.Equals(shortName, _request.ManagedStatic.Type, StringComparison.Ordinal))
                {
                    continue;
                }

                // Reaching the expected managed type is what identifies this chain as the real one,
                // so this is also where an undecided node.scriptInstance becomes decided.
                if (!_offsets.ContainsKey(OffsetKeys.NodeScriptInstance))
                {
                    Record(OffsetKeys.NodeScriptInstance, slot);
                    Record(OffsetKeys.ScriptInstanceOwner, backref);
                    _structuralEvidence[OffsetKeys.NodeScriptInstance] =
                        $"slot holding a pointer whose +0x{backref:x} is the node itself, chosen from "
                        + $"{chains.Count} such candidates because only this one's GCHandle dereferences to a "
                        + $"managed \"{shortName}\"";
                    _notes.Add(
                        $"node.scriptInstance resolved to 0x{slot:x} (owner backref 0x{backref:x}) by the managed "
                        + "bridge rather than by pointer identity alone; on a scene where only the root is scripted "
                        + "there is not enough pointer evidence to separate the candidates.");
                }

                Record(OffsetKeys.ScriptInstanceGcHandle, gcHandleOffset);
                _notes.Add(
                    "bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> "
                    + "GCHandle) and its type confirmed against the name the harness supplied. The static field slot "
                    + "itself was not independently resolved — LiveClr does not publish static addresses — so "
                    + "staticRootField is echoed from the request, not derived.");

                return new ManagedBridgeResult
                {
                    StaticRootType = shortName,
                    StaticRootField = _request.ManagedStatic.Field,
                    NativePtr = Wire.Pointer(info.NativePtr),
                    Reverse = new ReverseChain
                    {
                        OwnerBackref = owner == 0 ? null : Wire.Pointer(owner),
                        GcHandle = Wire.Pointer(handleSlot),
                    },
                    Fields = info.Fields,
                };
            }
        }

        _notes.Add($"bridge.managed: none of {chains.Count} candidate ScriptInstance chain(s) dereferenced to a "
                 + $"managed object of type \"{_request.ManagedStatic.Type}\".");
        return null;
    }

    // -- reporting ------------------------------------------------------------

    private List<NodeRecord> BuildNodes(WalkedScene scene, Dictionary<ulong, MemoryWindow> windows)
    {
        bool parentDerived = _offsets.ContainsKey(OffsetKeys.NodeParent);

        List<NodeRecord> records = [];
        foreach (WalkedNode node in scene.Nodes)
        {
            windows.TryGetValue(node.Address, out MemoryWindow? window);
            bool control = IsCanvasItem(node.Address) && ReadsAsControl(window);

            bool? visible = null;
            if (control
                && window is not null
                && _offsets.TryGetValue(OffsetKeys.CanvasItemVisible, out int visibleOffset)
                && window.TryByte(visibleOffset, out byte flag))
            {
                visible = flag != 0;
            }

            records.Add(new NodeRecord
            {
                Name = node.Name,
                // Null, not a guess. The geometry inference can only ever say "Control" or "Node",
                // which made 19 of 20 authored classes wrong on one run — ColorRect, Panel, Label and
                // RichTextLabel all reported as Control. A supertype is not a lesser answer, it is a
                // different claim, and class identity now gates text.
                NodeClass = _classes?.Names.GetValueOrDefault(node.Address),
                ClassSource = _classes?.Names.ContainsKey(node.Address) == true ? "engine" : null,
                NativePtr = Wire.Pointer(node.Address),
                // node.parent withheld means this was never read through a derived offset. One run
                // published the same image-range address as every node's parent because the walk
                // still carried whatever the un-derived slot happened to hold. A field with no
                // derivation behind it must be absent, not merely wrong-looking.
                ParentPtr = parentDerived && node.Parent != 0 ? Wire.Pointer(node.Parent) : null,
                ChildPtrs = [.. node.Children.Select(Wire.Pointer)],
                Size = control ? ReadVector(window, OffsetKeys.ControlSize, 2) : null,
                Position = control ? ReadVector(window, OffsetKeys.ControlPosition, 2) : null,
                Scale = control ? ReadVector(window, OffsetKeys.ControlScale, 2) : null,
                Offset = control ? ReadVector(window, OffsetKeys.ControlOffset, 4) : null,
                Visible = visible,
                Text = TextFor(node.Address),
            });
        }

        return records;
    }

    /// <summary>
    /// Whether the engine says this node has a <c>CanvasItem</c> at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Geometry plausibility cannot answer this and never could: a bare <c>Node</c> read through
    /// Control offsets came back <c>size=[0,0] scale=[0,0] offset=[0,0,0,0] visible=false</c>, and
    /// every one of those is a perfectly plausible reading. One run published <c>visible=true</c> for
    /// an object with no <c>CanvasItem</c> base at all. §12.4c is exactly this — a Control field read
    /// off a non-Control succeeds and returns garbage — and zeros are the shape of garbage that
    /// plausibility cannot catch.
    /// </para>
    /// <para>
    /// The class hierarchy is read from the target's own RTTI rather than compared against a list of
    /// Godot class names, so a custom class deriving from Control is handled without knowing its
    /// name. Where the hierarchy cannot be read, this returns false and the geometry is withheld:
    /// not knowing whether a node has these fields is not a licence to publish them.
    /// </para>
    /// </remarks>
    private bool IsCanvasItem(ulong node) => _classes?.DescendsFrom(node, "CanvasItem") == true;

    /// <summary>
    /// Whether this node's bytes read as a <c>Control</c> at the derived offsets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scene tree is not all Controls, and Control offsets applied to something else do not fail —
    /// they succeed and return nonsense. §12.4c watched exactly that: accessors on an
    /// <c>AudioStreamPlayer</c> returned denormals like <c>2.6e-38</c>, because an x64 heap pointer's
    /// high half decodes as a near-denormal float. Occasionally the nonsense is Infinity or NaN,
    /// which used to take the entire cell down on the way out through JSON.
    /// </para>
    /// <para>
    /// So geometry is published only where it reads as geometry, and <c>CanvasItem::visible</c> goes
    /// with it: that offset was derived from Control samples, and reading it on a plain <c>Node</c>
    /// lands somewhere past the end of a smaller object. Whether that byte happened to be mapped is
    /// not a fact about visibility, and letting it decide produced a flag that changed between runs
    /// on an unchanging target.
    /// </para>
    /// </remarks>
    private bool ReadsAsControl(MemoryWindow? window)
    {
        if (window is null)
        {
            return false;
        }

        int tested = 0;
        foreach ((string key, int arity) in
                 new[] { (OffsetKeys.ControlSize, 2), (OffsetKeys.ControlPosition, 2), (OffsetKeys.ControlOffset, 4) })
        {
            if (!_offsets.ContainsKey(key))
            {
                continue;
            }

            tested++;
            if (ReadVector(window, key, arity) is null)
            {
                return false;
            }
        }

        return tested > 0;
    }

    /// <summary>
    /// A node's text: a fresh read through the published class offset, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two properties come from doing it this way rather than from replaying what the discovery pass
    /// happened to see.
    /// </para>
    /// <para>
    /// It is <b>independently gated</b>. The eligibility test here is the node's engine class, read
    /// from the target — not anything <see cref="PublishClassText"/> wrote. A probe that bypassed the
    /// class filter entirely and fed a hostile pool straight to publication got text on every node,
    /// because the old interlock keyed on <c>_offsets</c>, which publication itself writes. One
    /// deleted line reinstated every phantom. This gate does not share a source with the thing it
    /// guards.
    /// </para>
    /// <para>
    /// And it <b>re-reads</b>. The discovery pass records what decoded at the moment it looked, so a
    /// single flaky read there was silently indistinguishable from a node having no text — which is
    /// why the same stable offset decoded one Label on one run and the other Label on the next,
    /// against an unchanging binary. The offset was never in doubt; only the reading was.
    /// </para>
    /// </remarks>
    private string? TextFor(ulong node)
    {
        if (_classes is null || !_classes.Names.TryGetValue(node, out string? className))
        {
            return null;
        }

        string key = className switch
        {
            "Label" => OffsetKeys.LabelText,
            "RichTextLabel" => OffsetKeys.RichTextLabelText,
            _ => string.Empty,
        };

        if (key.Length == 0 || !_offsets.TryGetValue(key, out int offset))
        {
            return null;
        }

        return TextCalibrator.TryReadTextField(_reader, node + (ulong)offset, out string value, out _)
            ? value
            : null;
    }

    private double[]? ReadVector(MemoryWindow? window, string key, int arity)
    {
        if (window is null || !_offsets.TryGetValue(key, out int offset))
        {
            return null;
        }

        double[] values = new double[arity];
        if (!window.TryReals(offset, _precision, arity, values))
        {
            return null;
        }

        // A reading that is not a coordinate is not a reading. Reporting it would publish a pointer
        // half as geometry, and where it is Infinity or NaN it cannot even be serialised.
        return Array.TrueForAll(values, SemanticCalibrator.IsPlausibleReading) ? values : null;
    }

    private void Record(string key, int offset) => _offsets[key] = offset;

    private void Publish(string key, OffsetCandidates result, Dictionary<string, string>? evidence, string? evidenceText)
    {
        _candidates[key] = [.. result.Candidates.Select(Wire.Offset)];
        _samples[key] = result.SampleCount;

        if (result.TryGetOffset(out int offset))
        {
            _offsets[key] = offset;
            if (evidence is not null && evidenceText is not null)
            {
                evidence[key] = evidenceText;
            }

            return;
        }

        _notes.Add($"{key}: not derived — {result.Obstacle()}.");
    }

    private DriverResult Build(int walkCount, IReadOnlyList<NodeRecord> nodes, ManagedBridgeResult? bridge)
    {
        Dictionary<string, string> Group(params string[] keys)
            => keys.Where(_offsets.ContainsKey).ToDictionary(k => k, k => Wire.Offset(_offsets[k]));

        return new DriverResult
        {
            EngineVersion = _request.EngineVersion,
            WalkCount = walkCount,
            Derivation = new Derivation
            {
                Structural = new StructuralDerivation
                {
                    Offsets = Group(OffsetKeys.NodeParent, OffsetKeys.NodeChildListHead, OffsetKeys.NodeScriptInstance),
                    Evidence = _structuralEvidence,
                },
                Semantic = new SemanticDerivation
                {
                    Offsets = Group(
                        OffsetKeys.ControlSize, OffsetKeys.ControlPosition, OffsetKeys.ControlScale,
                        OffsetKeys.ControlOffset, OffsetKeys.CanvasItemVisible),
                    Samples = _samples.Where(kv => _offsets.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
                    Candidates = _candidates,
                },
                Strings = new StringsDerivation
                {
                    Offsets = Group(OffsetKeys.NodeName, OffsetKeys.LabelText, OffsetKeys.RichTextLabelText),
                    Evidence = _stringEvidence,
                },
                Walk = new WalkDerivation
                {
                    ScriptInstanceClass = _scriptInstanceClass,
                    Offsets = Group(
                        OffsetKeys.ChildListNext, OffsetKeys.ChildListNode,
                        OffsetKeys.ScriptInstanceOwner, OffsetKeys.ScriptInstanceGcHandle),
                },
            },
            Nodes = nodes,
            ManagedBridge = bridge,
            ProfileCrossCheck = CrossCheck(),
            Notes = _notes,
        };
    }

    /// <summary>
    /// Compares the finished derivation against the shipped table — <b>after</b> every offset above
    /// is fixed, and with no path back into any of them.
    /// </summary>
    /// <remarks>
    /// §8.9's <c>calibration.unaided</c> is the check the grid exists to make, so this deliberately
    /// runs last, writes to a field the harness's schema does not know, and reports divergence
    /// rather than resolving it. §4.6's numbers came from a modified 4.5.1 engine; a disagreement
    /// with a stock template may indict either side and must not be silently absorbed by either.
    /// </remarks>
    private IReadOnlyDictionary<string, string>? CrossCheck()
    {
        if (_offsets.Count == 0)
        {
            return null;
        }

        Godot.External.Abi.GodotBuildTemplate template =
            string.Equals(_request.Cell.Template, "debug", StringComparison.OrdinalIgnoreCase)
                ? Godot.External.Abi.GodotBuildTemplate.Debug
                : Godot.External.Abi.GodotBuildTemplate.Release;

        Godot.External.Abi.GodotPrecision precision = _request.IsDoublePrecision
            ? Godot.External.Abi.GodotPrecision.Double
            : Godot.External.Abi.GodotPrecision.Single;

        if (!Godot.External.Abi.GodotAbiProfiles.TryGet(_request.Cell.Version, template, precision, out var profile))
        {
            return new Dictionary<string, string>
            {
                ["status"] = $"no shipped profile covers {_request.Cell.Name}; nothing to cross-check against, "
                           + "and nothing that could have been fallen back on.",
            };
        }

        Dictionary<string, int> shipped = new()
        {
            [OffsetKeys.NodeParent] = profile.Offsets.NodeParent,
            [OffsetKeys.NodeChildListHead] = profile.Offsets.NodeChildListHead,
            [OffsetKeys.NodeScriptInstance] = profile.Offsets.NodeScriptInstance,
            [OffsetKeys.NodeName] = profile.Offsets.NodeName,
            [OffsetKeys.CanvasItemVisible] = profile.Offsets.CanvasItemVisible,
            [OffsetKeys.ControlOffset] = profile.Offsets.ControlOffsets,
            [OffsetKeys.ControlScale] = profile.Offsets.ControlScale,
            [OffsetKeys.ControlPosition] = profile.Offsets.ControlPosition,
            [OffsetKeys.ControlSize] = profile.Offsets.ControlSize,
            [OffsetKeys.LabelText] = profile.Offsets.LabelText,
            [OffsetKeys.RichTextLabelText] = profile.Offsets.RichTextLabelText,
            [OffsetKeys.ChildListNext] = profile.Offsets.ChildLinkNext,
            [OffsetKeys.ChildListNode] = profile.Offsets.ChildLinkPayload,
            [OffsetKeys.ScriptInstanceOwner] = profile.Offsets.ScriptInstanceOwner,
            [OffsetKeys.ScriptInstanceGcHandle] = profile.Offsets.ScriptInstanceGcHandle,
        };

        Dictionary<string, string> report = [];
        int agreed = 0;
        foreach ((string key, int want) in shipped)
        {
            if (!_offsets.TryGetValue(key, out int got))
            {
                continue;
            }

            if (got == want)
            {
                agreed++;
            }
            else
            {
                report[key] = $"derived {Wire.Offset(got)}, shipped table says {Wire.Offset(want)}";
            }
        }

        report["status"] = report.Count == 0
            ? $"{agreed} derived offsets all agree with the shipped {_request.Cell.Version} table "
              + $"(confidence {profile.Confidence})"
            : $"{report.Count} disagreement(s) against the shipped {_request.Cell.Version} table; "
              + "resolve before quoting this cell as evidence — the table came from a MODIFIED 4.5.1 engine, "
              + "so it may be the table that is wrong.";

        return report;
    }

    private static Dictionary<string, ulong> MapPaths(WalkedScene scene, string rootName)
    {
        Dictionary<string, ulong> byPath = [];
        Dictionary<ulong, string> pathOf = [];

        foreach (WalkedNode node in scene.Nodes)
        {
            string path = node.Address == scene.Root?.Address
                ? rootName
                : pathOf.TryGetValue(node.Parent, out string? parentPath)
                    ? $"{parentPath}/{node.Name}"
                    : node.Name;

            pathOf[node.Address] = path;
            byPath.TryAdd(path, node.Address);
        }

        return byPath;
    }
}
