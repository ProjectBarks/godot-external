using Godot.External.Abi;

namespace Godot.External.Bench.Fixtures;

/// <summary>How a synthetic heap places its allocations, which is the whole experiment.</summary>
/// <param name="Name">Short label used in the report.</param>
/// <param name="Arenas">
/// Independent bump-allocation regions, 64 MiB apart. One arena means every allocation is adjacent
/// to the previous one; many arenas means consecutive allocations land far apart.
/// </param>
/// <param name="Locality">
/// Probability that an allocation continues in the arena the previous one used. 1.0 is a scene
/// instantiated in one uninterrupted burst; 0.0 is a heap that has been churning for an hour.
/// </param>
/// <param name="Seed">Fixed, so the fixture and every number derived from it are reproducible.</param>
internal sealed record HeapLayout(string Name, int Arenas, double Locality, int Seed = 20260817)
{
    /// <summary>A scene instantiated in one burst into a fresh arena: the best case for a page cache.</summary>
    public static HeapLayout Sequential { get; } = new("sequential", 1, 1.0);

    /// <summary>Mostly contiguous with occasional jumps — a plausible mid-session heap.</summary>
    public static HeapLayout Clustered { get; } = new("clustered", 16, 0.85);

    /// <summary>Allocation order unrelated to tree order: the worst case for a page cache.</summary>
    public static HeapLayout Scattered { get; } = new("scattered", 256, 0.0);

    /// <summary>The three layouts, weakest locality last.</summary>
    public static IReadOnlyList<HeapLayout> All { get; } = [Sequential, Clustered, Scattered];
}

/// <summary>A synthetic scene: the memory image, where its root is, and what a correct walk finds.</summary>
/// <param name="Memory">The heap.</param>
/// <param name="Root">Native <c>Node*</c> of the walk root.</param>
/// <param name="NodeCount">Nodes reachable from <paramref name="Root"/>.</param>
/// <param name="ControlCount">How many of them carry plausible <c>Control</c> geometry.</param>
/// <param name="DeepControl">A <c>Control</c> far from the root, for the targeted-read workload.</param>
/// <param name="SubtreeRoot">Root of a small subtree, for the polling workload.</param>
/// <param name="Layout">The layout that produced it.</param>
internal sealed record SyntheticScene(
    MemoryImage Memory,
    ulong Root,
    int NodeCount,
    int ControlCount,
    ulong DeepControl,
    ulong SubtreeRoot,
    HeapLayout Layout);

/// <summary>
/// Builds a Godot-shaped heap: nodes, intrusive child-list links, <c>StringName</c> data and UTF-32
/// character buffers, all laid out by a configurable allocator.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists to make cross-node locality a parameter instead of an assumption.</b> Per-node
/// locality is a fact about the ABI — one node's fields are within about 1.2 KB of each other, and
/// nothing can change that. Cross-node locality is a fact about Godot's allocator and about how long
/// the game has been running, and it decides whether a page cache is fetching three more nodes per
/// page or 4 KiB of somebody else's data. Sweeping it says exactly where each design wins.
/// </para>
/// <para>
/// Two details are copied from the real thing rather than idealised. Allocation is <b>depth-first</b>,
/// because that is the order Godot instantiates a scene, while the benchmark walks
/// <b>breadth-first</b>, because that is what <c>GodotScene.Walk</c> does — so even the perfectly
/// sequential layout does not hand the walk its nodes in address order. And link nodes and string
/// buffers are drawn from the <em>same</em> arenas as the node structs, so they interleave, which is
/// what stops a node span and its neighbours from being artificially adjacent.
/// </para>
/// </remarks>
internal static class SyntheticSceneBuilder
{
    /// <summary>
    /// Node struct size. The validated release profile puts <c>size_cache</c> at <c>0x4c0</c>, so a
    /// <c>Control</c> is at least <c>0x4c8</c>; rounded up to a 16-byte allocation quantum this is
    /// <c>0x528</c>, the figure the design brief works from.
    /// </summary>
    public const int NodeBytes = 0x528;

    /// <summary>An intrusive child-list link: <c>next</c> at <c>0x00</c>, payload at <c>0x18</c>.</summary>
    public const int LinkBytes = 0x20;

    /// <summary><c>StringName::_Data</c>: a small record whose <c>+0x08</c> is the character buffer.</summary>
    public const int StringNameDataBytes = 0x20;

    private const ulong ArenaStride = 64UL << 20;
    private const ulong HeapBase = 0x0000_0200_0000_0000UL;

    /// <summary>
    /// Builds a tree of <paramref name="nodeCount"/> nodes. The default is 2,341 — the live tree's
    /// peak in docs/analysis.md §12.4e.
    /// </summary>
    public static SyntheticScene Build(HeapLayout layout, int nodeCount = 2341, GodotAbiProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeCount, 2);

        profile ??= GodotAbiProfiles.Godot451Release;
        GodotOffsetTable offsets = profile.Offsets;

        MemoryImage memory = new();
        Random rng = new(layout.Seed);
        Allocator allocator = new(layout, rng);

        // Shape first, addresses second: the tree is decided before anything is placed, so the same
        // tree can be laid out three different ways and only the addresses differ.
        List<int> parentOf = [-1];
        List<List<int>> childrenOf = [[]];
        while (parentOf.Count < nodeCount)
        {
            int parent = ChooseParent(rng, parentOf.Count, childrenOf);
            int child = parentOf.Count;
            parentOf.Add(parent);
            childrenOf.Add([]);
            childrenOf[parent].Add(child);
        }

        // Depth-first, because that is the order Godot instantiates a scene tree.
        ulong[] address = new ulong[nodeCount];
        List<int> order = [];
        Stack<int> stack = new();
        stack.Push(0);
        while (stack.Count > 0)
        {
            int node = stack.Pop();
            order.Add(node);
            address[node] = allocator.Allocate(NodeBytes);

            for (int i = childrenOf[node].Count - 1; i >= 0; i--)
            {
                stack.Push(childrenOf[node][i]);
            }
        }

        int controls = 0;
        int deepest = 0;
        int deepestDepth = -1;

        foreach (int node in order)
        {
            ulong self = address[node];
            bool isControl = rng.NextDouble() < 0.6;
            if (isControl)
            {
                controls++;
            }

            memory.WriteUInt64(self + (ulong)offsets.NodeParent, parentOf[node] < 0 ? 0 : address[parentOf[node]]);
            memory.WriteUInt64(self + (ulong)offsets.NodeScriptInstance, 0);
            memory.Write(self + (ulong)offsets.CanvasItemVisible, [1]);

            // Name: node + NodeName -> _Data, _Data + 0x08 -> UTF-32 buffer, buffer - 8 -> count.
            string name = $"Node{node:D4}";
            ulong data = allocator.Allocate(StringNameDataBytes);
            ulong buffer = allocator.Allocate(8 + ((name.Length + 1) * 4)) + 8;
            memory.WriteUInt64(self + (ulong)offsets.NodeName, data);
            memory.WriteUInt64(data + (ulong)offsets.StringNameDataToBuffer, buffer);
            memory.WriteUInt64(buffer - (ulong)offsets.CowDataSizeBackOffset, (ulong)name.Length + 1);
            for (int i = 0; i < name.Length; i++)
            {
                memory.WriteUInt32(buffer + (ulong)(i * 4), name[i]);
            }

            memory.WriteUInt32(buffer + (ulong)(name.Length * 4), 0);

            WriteGeometry(memory, offsets, self, isControl, node, rng);

            // Child list, allocated as the children are attached — link nodes therefore interleave
            // with the children's own structs, exactly as they do in the engine.
            ulong previousLink = 0;
            foreach (int child in childrenOf[node])
            {
                ulong link = allocator.Allocate(LinkBytes);
                memory.WriteUInt64(link + (ulong)offsets.ChildLinkPayload, address[child]);
                memory.WriteUInt64(link + (ulong)offsets.ChildLinkNext, 0);

                if (previousLink == 0)
                {
                    memory.WriteUInt64(self + (ulong)offsets.NodeChildListHead, link);
                }
                else
                {
                    memory.WriteUInt64(previousLink + (ulong)offsets.ChildLinkNext, link);
                }

                previousLink = link;
            }

            if (previousLink == 0)
            {
                memory.WriteUInt64(self + (ulong)offsets.NodeChildListHead, 0);
            }

            int depth = Depth(parentOf, node);
            if (isControl && depth > deepestDepth)
            {
                deepestDepth = depth;
                deepest = node;
            }
        }

        // A small subtree for the polling workload: the deepest Control's great-grandparent, which
        // gives an overlay-sized cluster of a few dozen nodes rather than the whole tree.
        int subtree = deepest;
        for (int i = 0; i < 3 && parentOf[subtree] >= 0; i++)
        {
            subtree = parentOf[subtree];
        }

        return new SyntheticScene(memory, address[0], nodeCount, controls, address[deepest], address[subtree], layout);
    }

    private static int Depth(List<int> parentOf, int node)
    {
        int depth = 0;
        while (parentOf[node] >= 0)
        {
            node = parentOf[node];
            depth++;
        }

        return depth;
    }

    private static int ChooseParent(Random rng, int count, List<List<int>> childrenOf)
    {
        // Favours recent nodes, which produces a deep-ish UI tree rather than a wide flat one, and
        // caps sibling counts so no node ends up with hundreds of children.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            int candidate = Math.Max(0, count - 1 - (int)(rng.NextDouble() * rng.NextDouble() * count));
            if (childrenOf[candidate].Count < 6)
            {
                return candidate;
            }
        }

        return 0;
    }

    private static void WriteGeometry(
        MemoryImage memory,
        GodotOffsetTable offsets,
        ulong self,
        bool isControl,
        int node,
        Random rng)
    {
        if (isControl)
        {
            float x = 1 + (float)(rng.NextDouble() * 400);
            float y = 1 + (float)(rng.NextDouble() * 300);

            memory.WriteSingle(self + (ulong)offsets.ControlPosition, x);
            memory.WriteSingle(self + (ulong)offsets.ControlPosition + 4, y);
            memory.WriteSingle(self + (ulong)offsets.ControlSize, 16 + (node % 512));
            memory.WriteSingle(self + (ulong)offsets.ControlSize + 4, 16 + (node % 128));
            memory.WriteSingle(self + (ulong)offsets.ControlScale, 1);
            memory.WriteSingle(self + (ulong)offsets.ControlScale + 4, 1);
            memory.WriteSingle(self + (ulong)offsets.ControlGlobalPosition, x);
            memory.WriteSingle(self + (ulong)offsets.ControlGlobalPosition + 4, y);

            for (int i = 0; i < 4; i++)
            {
                memory.WriteSingle(self + (ulong)(offsets.ControlOffsets + (i * 4)), (i * 37) - 40);
            }

            return;
        }

        // A non-Control: whatever occupies those bytes is some other struct's business. Heap
        // pointers reinterpreted as real_t are exactly the denormals §12.4c measured (2.6e-38), so
        // the classifier has to reject this node for the same reason it does live.
        Span<byte> pointerish = stackalloc byte[8];
        for (int field = 0; field < 6; field++)
        {
            ulong fake = 0x0000_0217_4A2C_0000UL + ((ulong)node * 0x2000UL) + ((ulong)field * 8UL);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(pointerish, fake);
            memory.Write(self + (ulong)(offsets.ControlOffsets + (field * 8)), pointerish);
        }

        memory.Write(self + (ulong)offsets.ControlScale, pointerish);
        memory.Write(self + (ulong)offsets.ControlPosition, pointerish);
        memory.Write(self + (ulong)offsets.ControlSize, pointerish);
    }

    /// <summary>
    /// A bump allocator per arena. Arenas never overlap, so switching between them can never produce
    /// two objects at one address however the locality knob is turned.
    /// </summary>
    private sealed class Allocator
    {
        private readonly ulong[] _cursors;
        private readonly Random _rng;
        private readonly double _locality;
        private int _current;

        public Allocator(HeapLayout layout, Random rng)
        {
            _rng = rng;
            _locality = layout.Locality;
            _cursors = new ulong[layout.Arenas];
            for (int i = 0; i < _cursors.Length; i++)
            {
                _cursors[i] = HeapBase + ((ulong)i * ArenaStride);
            }
        }

        public ulong Allocate(int bytes)
        {
            if (_cursors.Length > 1 && _rng.NextDouble() >= _locality)
            {
                _current = _rng.Next(_cursors.Length);
            }

            ulong address = _cursors[_current];
            _cursors[_current] = (address + (ulong)bytes + 15UL) & ~15UL;
            return address;
        }
    }
}
