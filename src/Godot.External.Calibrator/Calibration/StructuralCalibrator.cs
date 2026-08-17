using LiveClr.Calibration;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>A simultaneous solution for <c>Node::name</c> and <c>Node::parent</c>.</summary>
public sealed record NodeAnchor(int NameOffset, int ParentOffset);

/// <summary>
/// §8.9 (a): the structural half. Pointer identity only — no value ever enters these derivations.
/// </summary>
/// <remarks>
/// A live address exists once in a process, so one sample can settle a structural offset where a
/// semantic one needs several (§12.5 derived <c>parent</c> at <c>0x128</c> from a single pair).
/// The gate that still applies is coverage: a slot that was never read cannot be ruled out, and
/// ruling out is how a unique answer is reached.
/// </remarks>
public sealed class StructuralCalibrator
{
    /// <summary>Plausible offsets for a link field inside a small intrusive-list node.</summary>
    public static IReadOnlyList<int> LinkOffsetCandidates { get; } = [0x00, 0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38];

    private readonly IMemoryReader _reader;
    private readonly StructuralProbe _probe;

    /// <summary>Creates a calibrator over a target reader.</summary>
    public StructuralCalibrator(IMemoryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _probe = new StructuralProbe(reader);
    }

    /// <summary>
    /// Solves <c>Node::name</c> and <c>Node::parent</c> together, from two <em>name slot</em>
    /// addresses whose nodes are known to be parent and child.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Neither node's base address is known at this point — a memory scan finds the slot holding the
    /// <c>StringName</c> of a node with a known name, which is <c>node + nameOffset</c> for an
    /// unknown <c>nameOffset</c>. Two unknowns, but only one scan is needed:
    /// </para>
    /// <code>
    /// child + parentOffset  holds  parent
    /// (A2 - n) + p          holds  (A1 - n)          with A1, A2 the two known slot addresses
    /// so for every candidate d = p - n:   n = A1 - *(A2 + d),  p = d + n
    /// </code>
    /// <para>
    /// Every slot in one window therefore proposes a complete <c>(name, parent)</c> pair, and the
    /// implausible ones fall out on range and alignment. It is pointer identity in both unknowns at
    /// once: nothing here looks at a value, a size or a name.
    /// </para>
    /// </remarks>
    public IReadOnlyList<NodeAnchor> SolveNameAndParent(ulong parentNameSlot, ulong childNameSlot, int maxNodeSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodeSize);

        List<NodeAnchor> solutions = [];
        ulong windowBase = childNameSlot > (ulong)maxNodeSize ? childNameSlot - (ulong)maxNodeSize : 0;
        MemoryWindow window = MemoryWindow.Read(_reader, windowBase, (2 * maxNodeSize) + 8);

        for (int slot = 0; slot + 8 <= window.Length; slot += 8)
        {
            if (!window.TryPointer(slot, out ulong value) || value == 0 || value > parentNameSlot)
            {
                continue;
            }

            ulong nameOffset = parentNameSlot - value;
            if (nameOffset > (ulong)maxNodeSize || nameOffset % 8 != 0)
            {
                continue;
            }

            long parentOffset = (long)(windowBase + (ulong)slot) - (long)childNameSlot + (long)nameOffset;
            if (parentOffset < 0 || parentOffset > maxNodeSize || parentOffset == (long)nameOffset)
            {
                continue;
            }

            NodeAnchor anchor = new((int)nameOffset, (int)parentOffset);
            if (!solutions.Contains(anchor))
            {
                solutions.Add(anchor);
            }
        }

        return solutions;
    }

    /// <summary>
    /// <c>Node::parent</c> across many child/parent pairs: the only slot in every child equal to
    /// that child's own parent.
    /// </summary>
    /// <remarks>
    /// The pairs come from candidate child lists, and a candidate list can name the same child
    /// twice — a wrong link offset walks a chain that revisits a node. <c>FindPointerOffsetAcross</c>
    /// rejects duplicate sample addresses by contract, so the duplicates are removed here rather
    /// than allowed to throw: a repeated child is a recoverable observation about the walk, and
    /// letting it escape as an exception would report a calibration condition as a crashed driver.
    /// </remarks>
    public OffsetCandidates DeriveParent(IReadOnlyList<(ulong Child, ulong Parent)> pairs, int scanBytes)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        List<(ulong Child, ulong Parent)> distinct = [.. pairs.DistinctBy(p => p.Child)];
        if (distinct.Count == 0)
        {
            return Empty("node.parent");
        }

        PointerSample[] samples = [.. distinct.Select(p => new PointerSample(p.Child, p.Parent))];

        CalibrationResult result;
        try
        {
            result = samples.Length == 1
                ? _probe.FindPointerOffset(samples[0].Address, samples[0].ExpectedTarget, scanBytes)
                : _probe.FindPointerOffsetAcross(samples, scanBytes);
        }
        catch (ArgumentException)
        {
            // Belt and braces. Whatever the probe refuses, refusing to derive is the verdict; an
            // exception out of here becomes a non-zero exit, which this driver reserves for "no pid".
            return Empty("node.parent");
        }

        return OffsetCandidates.From(
            result,
            distinct.Select(p => p.Child),
            distinct.Select(p => p.Parent.ToString("x", System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// <c>Node::childListHead</c> for one candidate link payload offset: the only slot <c>p</c> in
    /// the parent where <c>*(p + payload)</c> is a known child.
    /// </summary>
    /// <remarks>
    /// This is §12.5's sentence implemented literally, with the payload offset left as an unknown
    /// rather than assumed to be <c>0x18</c>. Only the list's <em>first</em> link is reachable from
    /// the node — later links hang off <c>next</c> — so a non-empty result also identifies which of
    /// the known children the authored scene puts first.
    /// </remarks>
    public OffsetCandidates DeriveChildListHead(ulong parent, ulong knownChild, int payloadOffset, int scanBytes)
        => OffsetCandidates.From(
            _probe.FindIndirectPointerOffset(parent, knownChild, payloadOffset, scanBytes),
            [parent],
            [knownChild.ToString("x", System.Globalization.CultureInfo.InvariantCulture)]);

    /// <summary>
    /// <c>Node::scriptInstance</c> for one candidate owner-backref offset: the only slot holding a
    /// pointer whose <c>+ backref</c> is the node itself.
    /// </summary>
    /// <remarks>
    /// §4.6: <c>node + 0x68</c> is Godot's <c>ScriptInstance</c>, not the managed object, and the
    /// <c>ScriptInstance</c>'s <c>+0x08</c> back-reference to its owner is a free self-check. Turning
    /// the self-check into the derivation itself makes both offsets fall out at once, and needs no
    /// managed side at all. Nodes without a script contribute nothing and are skipped rather than
    /// emptying the intersection.
    /// </remarks>
    public OffsetCandidates DeriveScriptInstance(IReadOnlyList<ulong> nodes, int backrefOffset, int scanBytes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        OffsetCandidates? accumulated = null;
        foreach (ulong node in nodes)
        {
            OffsetCandidates candidates = OffsetCandidates.From(
                _probe.FindIndirectPointerOffset(node, node, backrefOffset, scanBytes),
                [node],
                [node.ToString("x", System.Globalization.CultureInfo.InvariantCulture)]);

            if (candidates.IsEmpty)
            {
                continue; // scriptless node: absence of a script is not absence of the offset
            }

            accumulated = accumulated is null ? candidates : accumulated.Intersect(candidates);
        }

        return accumulated ?? Empty("node.scriptInstance");
    }

    private static OffsetCandidates Empty(string description)
        => new(description, [], CalibrationTechnique.Structural, [], [], false);
}
