using Godot.External.Abi;
using Godot.External.Memory;

namespace Godot.External.Values;

/// <summary>
/// Walks a Godot <c>Node</c>'s children, which are held as an <b>intrusive linked list</b> rather
/// than an array.
/// </summary>
/// <remarks>
/// <para>
/// Mechanism (docs/analysis.md §4.6, validated 4/4 on exact child-address <em>sequences</em> in
/// §12.3b):
/// </para>
/// <code>
/// cur = readPtr(node + NodeChildListHead)
/// while (cur != 0):
///     child = readPtr(cur + ChildLinkPayload)   // 0x18
///     cur   = readPtr(cur + ChildLinkNext)      // 0x00
/// </code>
/// <para>
/// <b>This walk can be silently wrong.</b> §12.4e: 100 paired traversals of a static tree agreed
/// perfectly and ~184,000 pointer chases produced zero read failures — but under structural
/// mutation one sample in 24 returned ten nodes short, with no exception and no failed read, because
/// a <c>next</c> pointer was sampled mid-splice. Roughly 4% of mutating samples tore and 0% of
/// static ones, so the risk is concentrated exactly where a live overlay is busiest.
/// </para>
/// <para>
/// Consequently the walk is bounded, cycle-checked, and reports suspicion instead of silently
/// returning a short list, and <see cref="WalkStable(IByteSource, GodotAbiProfile, ulong, int, int)"/>
/// implements §12.4e's recommended structural
/// check — agree-twice. A coherent page-cached snapshot (§4.7) would close most of the window
/// outright; until that exists, agree-twice is the mitigation.
/// </para>
/// </remarks>
internal static class ChildListWalk
{
    /// <summary>
    /// Bound on children per node. The live tree peaked around 2,341 nodes <em>in total</em>
    /// (§12.4e), so any single node claiming thousands of children is a corrupt chain, not a scene.
    /// </summary>
    public const int DefaultMaxChildren = 4096;

    /// <summary>Walks the child list of <paramref name="nodeAddress"/> once.</summary>
    public static ChildWalkResult Walk(
        IByteSource source,
        GodotAbiProfile profile,
        ulong nodeAddress,
        int maxChildren = DefaultMaxChildren)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Walk(source, profile.Offsets, nodeAddress, maxChildren);
    }

    /// <summary>Walks the child list of <paramref name="nodeAddress"/> once, against a raw offset table.</summary>
    public static ChildWalkResult Walk(
        IByteSource source,
        GodotOffsetTable offsets,
        ulong nodeAddress,
        int maxChildren = DefaultMaxChildren)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChildren);

        List<ulong> children = [];
        const ulong AlignmentMask = ByteSourceExtensions.PointerWidth - 1;

        if (!source.TryReadPointer(nodeAddress + (ulong)(long)offsets.NodeChildListHead, out ulong cursor))
        {
            return new ChildWalkResult(children, ChildWalkStatus.ReadFailed);
        }

        // Link-node addresses, not child addresses: the same child could legitimately never repeat,
        // but a repeated link is unambiguously a loop.
        HashSet<ulong> seenLinks = [];

        while (cursor != 0)
        {
            if ((cursor & AlignmentMask) != 0)
            {
                // Godot's allocator never hands out misaligned nodes; this is a torn or bogus read.
                return new ChildWalkResult(children, ChildWalkStatus.SuspectLink);
            }

            if (!seenLinks.Add(cursor))
            {
                return new ChildWalkResult(children, ChildWalkStatus.CycleDetected);
            }

            if (children.Count >= maxChildren)
            {
                return new ChildWalkResult(children, ChildWalkStatus.LimitExceeded);
            }

            if (!source.TryReadPointer(cursor + (ulong)(long)offsets.ChildLinkPayload, out ulong child))
            {
                return new ChildWalkResult(children, ChildWalkStatus.ReadFailed);
            }

            if (child == 0 || (child & AlignmentMask) != 0)
            {
                return new ChildWalkResult(children, ChildWalkStatus.SuspectLink);
            }

            children.Add(child);

            if (!source.TryReadPointer(cursor + (ulong)(long)offsets.ChildLinkNext, out ulong next))
            {
                return new ChildWalkResult(children, ChildWalkStatus.ReadFailed);
            }

            cursor = next;
        }

        return new ChildWalkResult(children, ChildWalkStatus.Complete);
    }

    /// <summary>
    /// Walks repeatedly and accepts the result only when two consecutive traversals produce the
    /// identical address sequence — the structural check §12.4e concluded is necessary on top of
    /// read-level retry.
    /// </summary>
    /// <param name="source">Target memory.</param>
    /// <param name="profile">Supplies the child-link offsets; never hardcoded here.</param>
    /// <param name="nodeAddress">Native <c>Node*</c> whose children are walked.</param>
    /// <param name="attempts">
    /// Maximum traversals. Two means "agree once or report <see cref="ChildWalkStatus.Unstable"/>";
    /// three gives one retry, which is appropriate while the tree is mutating.
    /// </param>
    /// <param name="maxChildren">Bound on the walk, so a corrupt link cannot spin.</param>
    /// <returns>
    /// The agreeing walk, or the last walk marked <see cref="ChildWalkStatus.Unstable"/> when no two
    /// consecutive traversals matched. A non-<see cref="ChildWalkStatus.Complete"/> walk is returned
    /// immediately: its own status is more informative than "unstable".
    /// </returns>
    public static ChildWalkResult WalkStable(
        IByteSource source,
        GodotAbiProfile profile,
        ulong nodeAddress,
        int attempts = 2,
        int maxChildren = DefaultMaxChildren)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return WalkStable(source, profile.Offsets, nodeAddress, attempts, maxChildren);
    }

    /// <inheritdoc cref="WalkStable(IByteSource, GodotAbiProfile, ulong, int, int)"/>
    public static ChildWalkResult WalkStable(
        IByteSource source,
        GodotOffsetTable offsets,
        ulong nodeAddress,
        int attempts = 2,
        int maxChildren = DefaultMaxChildren)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 2);

        ChildWalkResult previous = Walk(source, offsets, nodeAddress, maxChildren);
        if (!previous.IsComplete)
        {
            return previous;
        }

        // §6.4, learned twice in this project: inside a coherent snapshot the repeat traversal reads
        // the SAME frozen bytes, so it cannot disagree, and running it manufactures confidence out of
        // nothing while doubling the work. Freezing the image is the STRONGER mitigation — it closes
        // the mid-splice window instead of detecting it afterwards — so the weaker one steps aside
        // rather than being silently cancelled by it. The counter makes the substitution visible.
        if (source.IsCoherent())
        {
            source.NoteAgreeTwiceSuppressed();
            return previous;
        }

        for (int attempt = 1; attempt < attempts; attempt++)
        {
            ChildWalkResult current = Walk(source, offsets, nodeAddress, maxChildren);
            if (!current.IsComplete)
            {
                return current;
            }

            if (SameSequence(previous.Children, current.Children))
            {
                return current;
            }

            previous = current;
        }

        return new ChildWalkResult(previous.Children, ChildWalkStatus.Unstable);
    }

    private static bool SameSequence(IReadOnlyList<ulong> left, IReadOnlyList<ulong> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
