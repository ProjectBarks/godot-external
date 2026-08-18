using System.Buffers.Binary;
using System.Globalization;
using Godot.External.Abi;
using Godot.External.Calibrator.Target;
using Godot.External.Reflection;

namespace Godot.External.Calibrator.Reflection;

/// <summary>
/// One identified element of <c>ClassDB::classes</c>, plus the <c>StringName::_Data</c> layout that
/// was measured while identifying it.
/// </summary>
/// <param name="Element">A <c>HashMapElement*</c> in <c>ClassDB::classes</c>' intrusive chain.</param>
/// <param name="SeedClass">The class name the seed was searched for and read back.</param>
/// <param name="SeedMethod">The method whose <c>MethodBind</c> identified the element.</param>
/// <param name="NameOffset">Measured offset of <c>_Data::name</c>.</param>
/// <param name="CompileTimeNameOffset">Measured offset of <c>_Data::cname</c>, or −1.</param>
/// <param name="StructuralCandidates">How many structurally valid elements the scan produced.</param>
/// <param name="IdentifiedCandidates">How many of those survived identification. More than 1 is a refusal.</param>
/// <param name="Evidence">One line describing how the element was pinned down.</param>
internal sealed record ClassDbSeedResult(
    ulong Element,
    string SeedClass,
    string SeedMethod,
    int NameOffset,
    int CompileTimeNameOffset,
    int StructuralCandidates,
    int IdentifiedCandidates,
    string Evidence);

/// <summary>
/// Finds one element of <c>ClassDB::classes</c> in a live target, from nothing but a class name.
/// </summary>
/// <remarks>
/// <para>
/// <c>ClassDbElementWalk</c> can enumerate every registered class from any <em>one</em> element of the
/// chain, and its own documentation records that obtaining that element is the unsolved half, because
/// it needs a memory scan and no scanner lives in <c>Godot.External</c>. This is that half, and it
/// lives here because <see cref="RegionScanner"/> does.
/// </para>
/// <para>
/// <b>The chain.</b> A UTF-32 <c>"Label\0"</c> needle → CowData validation (the size word at
/// <c>ptr − sizeBack</c> must be exactly <c>len + 1</c>, and the refcount below it must be small and
/// positive) → pointers to that buffer, which are <c>&amp;_Data.name</c> → candidate <c>_Data</c>
/// bases at every plausible member offset → pointers to those, which are a <c>HashMapElement</c>'s
/// <c>data.key</c> at <c>element + 16</c> → a <c>prev</c>/<c>next</c> back-link round trip.
/// </para>
/// <para>
/// <b>Two false positives had to be closed first, and both are §13.11's species — a check with no way
/// to come out other than the way it came out.</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>"Pick the longest chain" is wrong.</b> It selects GDScript's <c>global_map</c> (1373 of 1419
/// entries on 4.3) and looked correct on 4.5 only by allocation luck. So the survivor is chosen by
/// <em>content</em>: the element's <c>ClassInfo</c> must hold a real <c>HashMap</c> containing a
/// <c>MethodBind</c> whose own <c>name</c> and <c>instance_class</c> read back as the method and
/// class searched for.
/// </item>
/// <item>
/// <b><c>_Data</c> objects are pool-allocated back to back</b>, so a candidate base one slot low
/// reads the <em>neighbouring</em> interned name — every key comes out a real, plausible, wrong
/// string, and identification still passed. Closed by requiring the seed element's own key to read
/// back as the very name that was searched for, under the candidate's own <c>name</c> offset.
/// </item>
/// </list>
/// <para>
/// Measured live on all eight 4.3/4.5 grid cells across multiple passes: <b>exactly 1 of 17
/// structurally valid candidates on 4.5 and 1 of 10 on 4.3</b>, with zero run-to-run drift
/// (docs/analysis.md §16.1).
/// </para>
/// </remarks>
internal static class ClassDbSeed
{
    /// <summary>
    /// Seeds to try, in order, stopping at the first that identifies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each pair is (class, a method <em>that class binds itself</em>). The method is what turns a
    /// structurally valid <c>HashMapElement</c> into an identified <c>ClassDB</c> element: any
    /// <c>StringName</c>-keyed map can hold a key spelled <c>"Label"</c>, but only
    /// <c>ClassDB::classes</c> has a <c>ClassInfo</c> behind it whose <c>method_map</c> holds a
    /// <c>MethodBind</c> for that class's own method.
    /// </para>
    /// <para>
    /// <b>Ordered by cost, and the list is short on purpose.</b> Each attempt is three passes over
    /// the target's private memory — roughly 600 MB and a few seconds — so a list that keeps trying
    /// after success would multiply the price of every calibration for nothing. <c>Label</c>
    /// identified on all eight cells and every pass; the other two are there so a scene that
    /// somehow perturbs the first has somewhere to go, not because the first is expected to fail.
    /// </para>
    /// <para>
    /// <c>Panel</c> and <c>VScrollBar</c> are deliberately <em>absent</em>: they bind no methods of
    /// their own, so they identify 0 of 5 / 0 of 4 — a true negative, correctly, and a wasted pass.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string Class, string Method)> DefaultSeeds { get; } =
    [
        ("Label", "get_text"),
        ("RichTextLabel", "get_text"),
        ("ColorRect", "get_color"),
    ];

    /// <summary>Widest <c>_Data</c> member offset considered for <c>name</c>, in qwords.</summary>
    /// <remarks>
    /// <c>_Data</c> is <c>{refcount, static_count, [cname, idx,] name, …}</c>; six qwords covers every
    /// 4.3–4.6 shape with room to spare. Widening it does not make a wrong candidate acceptable —
    /// identification still has to pass — it only costs scan time.
    /// </remarks>
    public const int MaxDataNameSlot = 6;

    private const int BufferHitLimit = 512;
    private const int PointerHitLimit = 8192;

    /// <summary>Runs the seed list until one class identifies.</summary>
    /// <param name="source">Target memory, as the byte seam the reflection module reads through.</param>
    /// <param name="scanner">Whole-process scanner over the target's private regions.</param>
    /// <param name="layout">Version-gated structure offsets.</param>
    /// <param name="binds">Resolves a class's own method binds by name; supplies identification.</param>
    /// <param name="seed">The identified element and the <c>_Data</c> layout measured with it.</param>
    /// <param name="reason">Why nothing was seeded. Empty on success.</param>
    /// <param name="seeds">Override for <see cref="DefaultSeeds"/>; used by tests.</param>
    public static bool TrySeed(
        IByteSource source,
        RegionScanner scanner,
        ClassDbLayout layout,
        MethodBindResolver binds,
        out ClassDbSeedResult? seed,
        out string reason,
        IReadOnlyList<(string Class, string Method)>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(binds);

        seed = null;
        List<string> attempts = [];

        foreach ((string className, string method) in seeds ?? DefaultSeeds)
        {
            if (TrySeedOne(source, scanner, layout, binds, className, method, out seed, out string why))
            {
                reason = string.Empty;
                return true;
            }

            attempts.Add(string.Format(CultureInfo.InvariantCulture, "{0}::{1} — {2}", className, method, why));
        }

        seed = null;
        reason = "no ClassDB::classes element could be identified: " + string.Join("; ", attempts);
        return false;
    }

    private static bool TrySeedOne(
        IByteSource source,
        RegionScanner scanner,
        ClassDbLayout layout,
        MethodBindResolver binds,
        string className,
        string ownMethod,
        out ClassDbSeedResult? seed,
        out string reason)
    {
        seed = null;

        // (a) the class name as Godot stores it: UTF-32, NUL-terminated.
        byte[] needle = new byte[(className.Length + 1) * 4];
        for (int i = 0; i < className.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(needle.AsSpan(i * 4), className[i]);
        }

        IReadOnlyList<ulong> raw = scanner.FindBytes(needle, BufferHitLimit);

        // CowData validation. The size word must be EXACTLY len+1 (Godot stores the NUL), which is
        // what rejects the tail of "RichTextLabel" when the needle is "Label", and the refcount below
        // it must be a small positive number.
        List<ulong> buffers = [];
        int sizeBack = layout.CowDataSizeBackOffset;
        foreach (ulong hit in raw)
        {
            if (hit < (ulong)(sizeBack * 2))
            {
                continue;
            }

            if (source.TryReadUInt64(hit - (ulong)sizeBack, out ulong size)
                && size == (ulong)className.Length + 1
                && source.TryReadUInt64(hit - (ulong)(sizeBack * 2), out ulong refcount)
                && refcount is > 0 and < 1_000_000)
            {
                buffers.Add(hit);
            }
        }

        if (buffers.Count == 0)
        {
            reason = $"{raw.Count} UTF-32 hit(s), none with a CowData header sized {className.Length + 1}";
            return false;
        }

        // (b) pointers to those buffers are `&_Data.name` (and any other String sharing the buffer).
        IReadOnlyDictionary<ulong, List<ulong>> bufferSlots = scanner.FindPointersTo(buffers, PointerHitLimit);

        // (c) candidate _Data bases: every plausible offset of `name` inside _Data.
        HashSet<ulong> candidates = [];
        foreach (ulong slot in bufferSlots.SelectMany(kv => kv.Value))
        {
            for (int k = 0; k <= MaxDataNameSlot; k++)
            {
                ulong candidate = slot - (ulong)(k * ByteSourceExtensions.PointerWidth);
                if (candidate < slot || k == 0)
                {
                    candidates.Add(candidate);
                }
            }
        }

        if (candidates.Count == 0)
        {
            reason = $"{buffers.Count} validated buffer(s), but nothing in memory points at them";
            return false;
        }

        // (d) pointers to a _Data base are a HashMapElement's data.key, at element + ElementData.
        IReadOnlyDictionary<ulong, List<ulong>> keyRefs = scanner.FindPointersTo(candidates, PointerHitLimit);

        // (e) the structural filter: an intact prev/next back-link round trip.
        List<(ulong Element, ulong Data, int NameOffset)> structural = [];
        foreach (KeyValuePair<ulong, List<ulong>> entry in keyRefs)
        {
            foreach (ulong slot in entry.Value)
            {
                if (slot < (ulong)layout.ElementData)
                {
                    continue;
                }

                ulong element = slot - (ulong)layout.ElementData;
                if (!BackLinksRoundTrip(source, element, layout))
                {
                    continue;
                }

                int nameOffset = NameOffsetWithin(entry.Key, bufferSlots);
                if (nameOffset >= 0)
                {
                    structural.Add((element, entry.Key, nameOffset));
                }
            }
        }

        if (structural.Count == 0)
        {
            reason = $"{keyRefs.Count} candidate _Data base(s), none referenced by a linked HashMapElement";
            return false;
        }

        // Identification, per candidate, with its OWN measured name offset — because the pool packs
        // _Data objects adjacently and a base one slot low reads the neighbour's name.
        List<(ClassDbSeedResult Seed, string Detail)> identified = [];
        foreach ((ulong element, ulong data, int nameOffset) in structural)
        {
            int cnameOffset = layout.StringNameHasCompileTimeName
                ? nameOffset - ByteSourceExtensions.PointerWidth
                : -1;
            InternedNameReader names = new(source, nameOffset, cnameOffset, layout.CowDataSizeBackOffset);

            // Gate one: the element's own key must read back as the name searched for.
            if (!ClassDbElementWalk.TryReadKeyPointer(source, element, layout, out ulong ownKey)
                || !names.TryRead(ownKey, out string ownName)
                || !string.Equals(ownName, className, StringComparison.Ordinal))
            {
                continue;
            }

            // Gate two: the chain must walk clean end to end.
            if (!ClassDbElementWalk.TryEnumerate(source, element, layout, out IReadOnlyList<ulong> chain, out _))
            {
                continue;
            }

            // Gate three: the value behind the key must be a ClassInfo carrying this class's own
            // named MethodBind. This is the gate that separates ClassDB::classes from every other
            // StringName-keyed map in the process.
            ulong classInfo = element + (ulong)layout.ElementData + ByteSourceExtensions.PointerWidth;
            if (!binds.TryResolve(classInfo, className, ownMethod, names, out ulong bind, out string bindEvidence))
            {
                continue;
            }

            identified.Add((
                new ClassDbSeedResult(
                    element,
                    className,
                    ownMethod,
                    nameOffset,
                    cnameOffset,
                    structural.Count,
                    0,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "element 0x{0:x} of {1} structurally valid candidate(s): its own key reads back as \"{2}\" "
                        + "through _Data.name@+0x{3:x}, the chain walks {4} element(s) clean, and its ClassInfo holds "
                        + "{5}::{6} (MethodBind 0x{7:x}; {8})",
                        element,
                        structural.Count,
                        className,
                        nameOffset,
                        chain.Count,
                        className,
                        ownMethod,
                        bind,
                        bindEvidence)),
                bindEvidence));
        }

        if (identified.Count == 0)
        {
            reason = $"{structural.Count} structurally valid candidate(s), none identified as ClassDB::classes";
            return false;
        }

        // More than one survivor is a refusal, not a coin flip. The whole point of identification is
        // that it leaves exactly one; two would mean the criterion is not the criterion it claims.
        if (identified.Count > 1)
        {
            reason = $"{identified.Count} of {structural.Count} candidates identified as ClassDB::classes; "
                + "the identification is not unique, so nothing is seeded";
            return false;
        }

        seed = identified[0].Seed with { IdentifiedCandidates = identified.Count };
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// The offset of <c>name</c> within a candidate <c>_Data</c> base — the distance to whichever
    /// buffer-pointer slot the base was derived from.
    /// </summary>
    private static int NameOffsetWithin(ulong data, IReadOnlyDictionary<ulong, List<ulong>> bufferSlots)
    {
        int best = -1;
        foreach (ulong slot in bufferSlots.SelectMany(kv => kv.Value))
        {
            if (slot < data)
            {
                continue;
            }

            ulong distance = slot - data;
            if (distance <= (ulong)(MaxDataNameSlot * ByteSourceExtensions.PointerWidth))
            {
                best = (int)distance;
            }
        }

        return best;
    }

    private static bool BackLinksRoundTrip(IByteSource source, ulong element, ClassDbLayout layout)
    {
        if ((element & 7) != 0)
        {
            return false;
        }

        if (!source.TryReadPointer(element + (ulong)layout.ElementNext, out ulong next)
            || !source.TryReadPointer(element + (ulong)layout.ElementPrevious, out ulong previous))
        {
            return false;
        }

        if (next == 0 && previous == 0)
        {
            return false;
        }

        if (next != 0
            && (!source.TryReadPointer(next + (ulong)layout.ElementPrevious, out ulong backPrevious)
                || backPrevious != element))
        {
            return false;
        }

        return previous == 0
            || (source.TryReadPointer(previous + (ulong)layout.ElementNext, out ulong backNext) && backNext == element);
    }
}
