using Godot.External.Values;

namespace Godot.External.Objects;

/// <summary>
/// A node's children, and how much the list can be trusted.
/// </summary>
/// <remarks>
/// <para>
/// The status travels <b>with</b> the list rather than being logged and dropped, because
/// docs/analysis.md §12.4e's failure is invisible in the list itself: under structural mutation a
/// walk returned 2296 nodes where the reference returned 2306, with "no exception, no failed read,
/// no <c>memory-access-exception</c>" — every pointer plausible, the list simply ten short. A caller
/// holding only <see cref="Nodes"/> is holding a quietly wrong answer roughly 4% of the time the
/// tree is mutating, which is exactly when an overlay is busiest.
/// </para>
/// <para>
/// So there is no property that hands over the children alone. Read <see cref="IsComplete"/> first.
/// </para>
/// </remarks>
internal sealed class NodeChildren
{
    internal NodeChildren(IReadOnlyList<GodotNode> nodes, ChildWalkStatus status)
    {
        Nodes = nodes;
        Status = status;
    }

    /// <summary>The children, in list order. <b>Possibly partial</b> — check <see cref="Status"/>.</summary>
    public IReadOnlyList<GodotNode> Nodes { get; }

    /// <summary>Why the underlying intrusive-list walk stopped.</summary>
    public ChildWalkStatus Status { get; }

    /// <summary>The walk terminated normally and two consecutive traversals agreed.</summary>
    public bool IsComplete => Status == ChildWalkStatus.Complete;

    /// <summary>
    /// The list is known to be short, looped, or unstable. §12.4e's recommended response is to
    /// re-sample or reuse the last good scene epoch — not to use this list.
    /// </summary>
    public bool LooksTruncatedOrLooped => !IsComplete;

    /// <summary>Number of children found. Not necessarily the number the node has.</summary>
    public int Count => Nodes.Count;

    /// <inheritdoc/>
    public override string ToString() => $"{Status} ({Count} children)";
}
