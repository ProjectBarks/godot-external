using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Values;

namespace Godot.External.Scene;

/// <summary>
/// The nodes found by a whole-tree walk, together with every structural doubt raised along the way.
/// </summary>
/// <remarks>
/// <para>
/// docs/analysis.md §12.4e is the design input for this type. A 4 Hz sampler traversed "2,308 nodes
/// × 80 iterations ≈ 184,000 pointer chases in 20 s with zero failed reads" — and yet, once the tree
/// started mutating, one sample in 24 came back ten nodes short with no error of any kind, because a
/// <c>next</c> pointer was sampled mid-splice. The final tally was "299 samples, 26 mutations, 1
/// mismatch, 0 read errors": roughly 4% of mutating samples tore, 0% of static ones.
/// </para>
/// <para>
/// That failure cannot be seen in the node list, so it is reported beside it. §4.8's
/// <c>isTransientRead</c> will not catch it either — every read succeeded. The caller's options are
/// re-sample, or reuse the last good scene epoch.
/// </para>
/// </remarks>
internal sealed class TreeWalkResult
{
    internal TreeWalkResult(
        IReadOnlyList<GodotNode> nodes,
        ChildWalkStatus worstStatus,
        IReadOnlyList<NativePtr> suspectNodes,
        bool hitNodeLimit)
    {
        Nodes = nodes;
        WorstStatus = worstStatus;
        SuspectNodes = suspectNodes;
        HitNodeLimit = hitNodeLimit;
    }

    /// <summary>
    /// Every node reached, root first, in breadth-first order. <b>Possibly incomplete</b> — check
    /// <see cref="IsComplete"/>.
    /// </summary>
    public IReadOnlyList<GodotNode> Nodes { get; }

    /// <summary>
    /// The most serious child-walk status seen anywhere in the tree, with
    /// <see cref="ChildWalkStatus.Unstable"/> ranked highest because it is §12.4e's tearing signal
    /// and the only one that read-level retry cannot reproduce.
    /// </summary>
    public ChildWalkStatus WorstStatus { get; }

    /// <summary>Nodes whose own child walk was not <see cref="ChildWalkStatus.Complete"/>.</summary>
    public IReadOnlyList<NativePtr> SuspectNodes { get; }

    /// <summary>The walk stopped at its node bound rather than exhausting the tree.</summary>
    public bool HitNodeLimit { get; }

    /// <summary>Every child walk completed and the bound was not hit.</summary>
    public bool IsComplete => WorstStatus == ChildWalkStatus.Complete && !HitNodeLimit;

    /// <summary>Number of nodes reached.</summary>
    public int Count => Nodes.Count;

    /// <summary>
    /// Ranks a status by how badly it undermines the result, so a tree-wide walk can report the worst
    /// thing that happened rather than the last.
    /// </summary>
    internal static int Severity(ChildWalkStatus status) => status switch
    {
        ChildWalkStatus.Complete => 0,
        ChildWalkStatus.LimitExceeded => 1,
        ChildWalkStatus.ReadFailed => 2,
        ChildWalkStatus.CycleDetected => 3,
        ChildWalkStatus.SuspectLink => 4,
        ChildWalkStatus.Unstable => 5,
        _ => 6,
    };

    /// <inheritdoc/>
    public override string ToString()
        => $"{WorstStatus} ({Count} nodes, {SuspectNodes.Count} suspect{(HitNodeLimit ? ", hit node limit" : string.Empty)})";
}
