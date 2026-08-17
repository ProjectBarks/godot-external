using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>One candidate answer for the whole node-level layout, before it has been believed.</summary>
public sealed record CandidateLayout(
    int NameOffset,
    int ParentOffset,
    int ChildListHead,
    int ChildLinkNext,
    int ChildLinkPayload,
    int StringNameDataToBuffer);

/// <summary>A node as the walk found it.</summary>
public sealed record WalkedNode(ulong Address, ulong Parent, IReadOnlyList<ulong> Children, string Name);

/// <summary>The result of walking a candidate layout.</summary>
public sealed record WalkedScene(IReadOnlyList<WalkedNode> Nodes, bool HitLimit)
{
    /// <summary>Nodes by address.</summary>
    public IReadOnlyDictionary<ulong, WalkedNode> ByAddress { get; }
        = Nodes.GroupBy(n => n.Address).ToDictionary(g => g.Key, g => g.First());

    /// <summary>The node the walk started from.</summary>
    public WalkedNode? Root => Nodes.Count > 0 ? Nodes[0] : null;
}

/// <summary>
/// Walks the intrusive child list with a <em>candidate</em> layout, and decides whether the result
/// is a tree at all.
/// </summary>
/// <remarks>
/// The search over link offsets needs a verdict per combination, and "did it reproduce the authored
/// scene" is the only honest one available: the right combination reaches exactly the node count the
/// harness stated, with exactly the names it listed, and every child's parent pointer agreeing with
/// the list that produced it. A wrong combination either walks into nothing or produces a shape that
/// fails one of those, which is why the check is used to <em>select</em> the layout rather than to
/// grade it afterwards.
/// </remarks>
public static class SceneWalker
{
    /// <summary>Bound on children per node; a longer chain means a misread link, not a big scene.</summary>
    public const int MaxChildren = 4096;

    /// <summary>Walks breadth-first from <paramref name="root"/>.</summary>
    public static WalkedScene Walk(IMemoryReader reader, ulong root, CandidateLayout layout, int maxNodes)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        List<WalkedNode> nodes = [];
        HashSet<ulong> visited = [root];
        Queue<ulong> queue = new();
        queue.Enqueue(root);
        bool hitLimit = false;

        while (queue.Count > 0)
        {
            ulong address = queue.Dequeue();
            if (nodes.Count >= maxNodes)
            {
                hitLimit = true;
                break;
            }

            reader.TryReadPointer(address + (ulong)layout.ParentOffset, out ulong parent);
            GodotText.TryReadStringName(
                reader,
                address + (ulong)layout.NameOffset,
                layout.StringNameDataToBuffer,
                out string name);

            List<ulong> children = ReadChildren(reader, address, layout);
            nodes.Add(new WalkedNode(address, parent, children, name));

            foreach (ulong child in children)
            {
                if (visited.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return new WalkedScene(nodes, hitLimit);
    }

    /// <summary>Follows one node's child list.</summary>
    public static List<ulong> ReadChildren(IMemoryReader reader, ulong node, CandidateLayout layout)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(layout);

        List<ulong> children = [];
        HashSet<ulong> seenLinks = [];

        if (!reader.TryReadPointer(node + (ulong)layout.ChildListHead, out ulong link))
        {
            return children;
        }

        while (link != 0 && children.Count < MaxChildren && seenLinks.Add(link))
        {
            if (!reader.TryReadPointer(link + (ulong)layout.ChildLinkPayload, out ulong child) || child == 0 || (child & 7) != 0)
            {
                break;
            }

            children.Add(child);

            if (!reader.TryReadPointer(link + (ulong)layout.ChildLinkNext, out link))
            {
                break;
            }
        }

        return children;
    }

    /// <summary>
    /// Whether a walk reproduced the authored scene: the stated node count, the stated names, and
    /// parent pointers that round-trip against the child lists.
    /// </summary>
    /// <remarks>
    /// <paramref name="verifyParents"/> is false while the parent offset is still unknown: the
    /// child-list search runs before the pointer-identity pass that derives <c>node.parent</c>, so
    /// demanding the round-trip then would reject the very layout that makes it checkable.
    /// </remarks>
    public static bool Reproduces(
        WalkedScene scene,
        int expectedNodeCount,
        IReadOnlyCollection<string> expectedNames,
        bool verifyParents = true)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(expectedNames);

        if (scene.HitLimit || scene.Nodes.Count != expectedNodeCount)
        {
            return false;
        }

        if (expectedNames.Count > 0)
        {
            HashSet<string> wanted = [.. expectedNames];
            HashSet<string> found = [.. scene.Nodes.Select(n => n.Name)];
            if (!wanted.SetEquals(found))
            {
                return false;
            }
        }

        foreach (WalkedNode node in scene.Nodes)
        {
            foreach (ulong child in node.Children)
            {
                if (!scene.ByAddress.TryGetValue(child, out WalkedNode? record))
                {
                    return false;
                }

                if (verifyParents && record.Parent != node.Address)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
