using System.Diagnostics.CodeAnalysis;
using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Values;

namespace Godot.External.Scene;

/// <summary>
/// A view of the scene tree from one root, scoped to a <see cref="SceneEpoch"/>.
/// </summary>
/// <remarks>
/// <para>
/// Traversal is built on <c>Values/ChildListWalk</c> and <b>surfaces its verdict</b> rather than
/// swallowing it. docs/analysis.md §12.4e found that an intrusive-list walk can terminate early with
/// no exception, no failed read, and every pointer plausible; a scene API that returned a bare list
/// of nodes would hide exactly the one bug that changed the design.
/// </para>
/// <para>
/// The walk is breadth-first, bounded, and cycle-checked on node addresses. The bound matters for the
/// same reason the epoch does: a freed-and-reused allocation (§8.8) can produce a child pointer back
/// into an already-visited node, and the resulting graph is not a tree.
/// </para>
/// </remarks>
internal sealed class GodotScene
{
    /// <summary>
    /// Default bound on nodes visited. The live tree peaked around 2,341 nodes total (§12.4e), so
    /// this leaves room for a much larger game while still terminating on a corrupt graph.
    /// </summary>
    public const int DefaultMaxNodes = 16384;

    internal GodotScene(GodotNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    /// <summary>The root the tree is walked from.</summary>
    public GodotNode Root { get; }

    /// <summary>The epoch this view and every node it produces belong to.</summary>
    public SceneEpoch Epoch => Root.Epoch;

    /// <summary>
    /// Walks the whole tree breadth-first.
    /// </summary>
    /// <param name="maxNodes">Bound on nodes visited.</param>
    /// <param name="attempts">
    /// Traversals per child list for the agree-twice check (§12.4e). Two detects tearing; three
    /// gives one retry.
    /// </param>
    /// <returns>
    /// The nodes reached and every structural doubt raised. A torn walk still returns its nodes —
    /// callers that want the data anyway can have it, but they cannot avoid seeing that it is suspect.
    /// </returns>
    public TreeWalkResult Walk(int maxNodes = DefaultMaxNodes, int attempts = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);
        Epoch.EnsureCurrent();

        List<GodotNode> nodes = [];
        List<NativePtr> suspect = [];
        HashSet<ulong> visited = [];
        Queue<GodotNode> queue = new();

        ChildWalkStatus worst = ChildWalkStatus.Complete;
        bool hitLimit = false;

        queue.Enqueue(Root);
        visited.Add(Root.Address.Address);

        while (queue.Count > 0)
        {
            GodotNode node = queue.Dequeue();
            nodes.Add(node);

            if (nodes.Count >= maxNodes)
            {
                hitLimit = true;
                break;
            }

            NodeChildren children = node.GetChildren(attempts);
            if (!children.IsComplete)
            {
                suspect.Add(node.Address);
                if (TreeWalkResult.Severity(children.Status) > TreeWalkResult.Severity(worst))
                {
                    worst = children.Status;
                }
            }

            foreach (GodotNode child in children.Nodes)
            {
                // A repeat means the "tree" has a cycle — see the remarks on this class.
                if (visited.Add(child.Address.Address))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return new TreeWalkResult(nodes, worst, suspect, hitLimit);
    }

    /// <summary>
    /// Breadth-first search for the first node matching <paramref name="predicate"/>.
    /// </summary>
    /// <remarks>
    /// Uses the same bounded, agree-twice traversal as <see cref="Walk"/>, so a search that crosses a
    /// torn child list can miss a node that exists. If that matters, walk and inspect
    /// <see cref="TreeWalkResult.WorstStatus"/> instead of searching.
    /// </remarks>
    public bool TryFind(
        Func<GodotNode, bool> predicate,
        [NotNullWhen(true)] out GodotNode? found,
        int maxNodes = DefaultMaxNodes,
        int attempts = 2)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);
        Epoch.EnsureCurrent();

        HashSet<ulong> visited = [Root.Address.Address];
        Queue<GodotNode> queue = new();
        queue.Enqueue(Root);

        int visitedCount = 0;

        while (queue.Count > 0 && visitedCount < maxNodes)
        {
            GodotNode node = queue.Dequeue();
            visitedCount++;

            if (predicate(node))
            {
                found = node;
                return true;
            }

            foreach (GodotNode child in node.GetChildren(attempts).Nodes)
            {
                if (visited.Add(child.Address.Address))
                {
                    queue.Enqueue(child);
                }
            }
        }

        found = null;
        return false;
    }

    /// <summary>
    /// Finds the first node whose <c>Node::name</c> equals <paramref name="name"/> (ordinal).
    /// </summary>
    /// <remarks>
    /// Names are not unique in a Godot tree except among siblings, so this returns the first in
    /// breadth-first order. Nodes whose name cannot be read simply do not match — a failed read must
    /// not be reported as a name collision.
    /// </remarks>
    public bool TryFindByName(
        string name,
        [NotNullWhen(true)] out GodotNode? found,
        int maxNodes = DefaultMaxNodes,
        int attempts = 2)
    {
        ArgumentNullException.ThrowIfNull(name);

        return TryFind(
            node => node.TryGetName(out string candidate) && string.Equals(candidate, name, StringComparison.Ordinal),
            out found,
            maxNodes,
            attempts);
    }

    /// <inheritdoc/>
    public override string ToString() => $"GodotScene(root {Root.Address}) @epoch#{Epoch.Id}";
}
