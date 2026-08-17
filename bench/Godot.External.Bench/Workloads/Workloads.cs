using Godot.External.Bench.Measurement;
using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Bench.Workloads;

/// <summary>Where a workload starts from. Supplied by whichever target the run is against.</summary>
/// <param name="Root">Walk root.</param>
/// <param name="Node">A single node for the targeted-read workload; ideally a deep <c>Control</c>.</param>
/// <param name="Subtree">Root of a small subtree for the polling workload.</param>
internal sealed record WorkloadAnchors(NativePtr Root, NativePtr Node, NativePtr Subtree);

/// <summary>
/// What a workload produced, reduced to something comparable. Every variant must return the same
/// result, or the cache is fast and wrong.
/// </summary>
/// <param name="Items">Nodes or values visited — the primary correctness check.</param>
/// <param name="Checksum">
/// Order-sensitive hash of everything read. A cache that serves the right bytes from the wrong
/// address, or drops a read that the uncached path served, changes this.
/// </param>
/// <param name="Note">Free text for the report, e.g. the walk's structural verdict.</param>
internal readonly record struct WorkloadResult(int Items, ulong Checksum, string Note);

/// <summary>One measurable operation, run identically under every cache variant.</summary>
internal interface IWorkload
{
    /// <summary>Short label used in the report.</summary>
    string Name { get; }

    /// <summary>What it does and why it is in the set.</summary>
    string Description { get; }

    /// <summary>Runs it. Every snapshot must be opened through <paramref name="context"/>.</summary>
    WorkloadResult Run(BenchContext context, WorkloadAnchors anchors);
}

/// <summary>Accumulates an order-sensitive checksum of everything a workload observed.</summary>
internal sealed class Checksum
{
    private ulong _value = 0xcbf29ce484222325UL;

    /// <summary>The value so far.</summary>
    public ulong Value => _value;

    /// <summary>Folds a 64-bit value in (FNV-1a).</summary>
    public void Add(ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            _value = (_value ^ ((value >> (i * 8)) & 0xff)) * 0x100000001b3UL;
        }
    }

    /// <summary>Folds a string in.</summary>
    public void Add(string value)
    {
        foreach (char c in value)
        {
            Add(c);
        }

        Add(0xfeedUL);
    }

    /// <summary>Folds a coordinate in, rounded so float noise cannot make two variants disagree.</summary>
    public void Add(double value) => Add((ulong)(long)Math.Round(value * 1024));
}

/// <summary>
/// (a) The full scene-tree walk: the most pointer-chase-heavy thing this library does.
/// </summary>
/// <remarks>
/// Structure <em>and</em> names, because that is what a consumer actually asks for and because the
/// two have opposite locality. The structural walk stays inside node structs and link nodes; the
/// name of each node is two further hops into unrelated allocations — a <c>StringName::_Data</c>
/// and a UTF-32 buffer — which nothing about the node span can help with. A cache that only looks
/// good on the structural half would be measuring the easy part.
/// </remarks>
internal sealed class FullTreeWalkWorkload : IWorkload
{
    /// <inheritdoc/>
    public string Name => "walk";

    /// <inheritdoc/>
    public string Description => "full breadth-first tree walk, reading every node's name";

    /// <inheritdoc/>
    public WorkloadResult Run(BenchContext context, WorkloadAnchors anchors)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(anchors);

        Checksum checksum = new();
        int nodes = 0;
        string note = string.Empty;

        context.InSnapshot(_ =>
        {
            GodotScene scene = context.Epoch.SceneFrom(anchors.Root);
            TreeWalkResult walk = scene.Walk();
            nodes = walk.Nodes.Count;
            note = walk.WorstStatus.ToString();

            foreach (GodotNode node in walk.Nodes)
            {
                checksum.Add(node.Address.Address);
                if (node.TryGetName(out string name))
                {
                    checksum.Add(name);
                }
            }
        });

        return new WorkloadResult(nodes, checksum.Value, note);
    }
}

/// <summary>
/// (b) A targeted read of one node's geometry, repeated so the per-call cost is measurable.
/// </summary>
/// <remarks>
/// The case where a cache has the least room to help and the most room to hurt: a handful of reads
/// into one struct, then a parent walk. A page cache pays 4 KiB for the first field; an object cache
/// pays about 1.2 KB; no cache pays 8 bytes several times over. Whichever wins here wins because of
/// the ancestor chain, which is the only part with more than one object in it.
/// </remarks>
internal sealed class TargetedGeometryWorkload(int iterations = 200) : IWorkload
{
    /// <inheritdoc/>
    public string Name => "geometry";

    /// <inheritdoc/>
    public string Description => $"{iterations}x read one node's full geometry and composed global position";

    /// <inheritdoc/>
    public WorkloadResult Run(BenchContext context, WorkloadAnchors anchors)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(anchors);

        Checksum checksum = new();
        int reads = 0;

        for (int i = 0; i < iterations; i++)
        {
            context.InSnapshot(_ =>
            {
                GodotNode node = context.Epoch.Node(anchors.Node);
                if (!node.TryAsControl(out GodotControl? control))
                {
                    return;
                }

                reads++;

                if (control.TryGetSize(out GodotVector2 size))
                {
                    checksum.Add(size.X);
                    checksum.Add(size.Y);
                }

                if (control.TryGetPosition(out GodotVector2 position))
                {
                    checksum.Add(position.X);
                    checksum.Add(position.Y);
                }

                if (control.TryGetScale(out GodotVector2 scale))
                {
                    checksum.Add(scale.X);
                    checksum.Add(scale.Y);
                }

                double[] offsets = new double[4];
                if (control.TryGetOffsets(offsets))
                {
                    foreach (double value in offsets)
                    {
                        checksum.Add(value);
                    }
                }

                if (control.TryGetVisible(out bool visible))
                {
                    checksum.Add(visible ? 1UL : 0UL);
                }

                if (control.TryGetGlobalPosition(out ComposedGlobalPosition global))
                {
                    checksum.Add(global.Position.X);
                    checksum.Add(global.Position.Y);
                    checksum.Add((ulong)global.AncestorsComposed);
                }
            });
        }

        return new WorkloadResult(reads, checksum.Value, $"{iterations} snapshots");
    }
}

/// <summary>
/// (c) The overlay's actual pattern: poll a small subtree repeatedly, one snapshot per poll.
/// </summary>
/// <remarks>
/// <para>
/// <b>One snapshot per poll is the design, not an implementation detail.</b> Reusing a snapshot
/// across polls would make every poll after the first free and every poll after the first wrong —
/// the exact failure docs/analysis.md §6.4 records, and the reason
/// <c>SceneEpoch.Snapshot</c> refuses to open a second snapshot while one is live. The cost of
/// rebuilding the cache 20 times is therefore part of the measurement, and it is the number that
/// decides whether caching is worth anything to an overlay at all.
/// </para>
/// <para>
/// Polls run back to back rather than at 4 Hz on a timer: the wall time being measured is the work,
/// not the interval between the work.
/// </para>
/// </remarks>
internal sealed class SubtreePollWorkload(int polls = 20, int maxNodes = 96) : IWorkload
{
    /// <inheritdoc/>
    public string Name => "poll";

    /// <inheritdoc/>
    public string Description => $"{polls}x poll a <={maxNodes}-node subtree (geometry + names), one snapshot each";

    /// <inheritdoc/>
    public WorkloadResult Run(BenchContext context, WorkloadAnchors anchors)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(anchors);

        Checksum checksum = new();
        int visited = 0;

        for (int poll = 0; poll < polls; poll++)
        {
            context.InSnapshot(_ =>
            {
                GodotScene scene = context.Epoch.SceneFrom(anchors.Subtree);
                TreeWalkResult walk = scene.Walk(maxNodes);

                foreach (GodotNode node in walk.Nodes)
                {
                    visited++;
                    checksum.Add(node.Address.Address);

                    if (node.TryGetName(out string name))
                    {
                        checksum.Add(name);
                    }

                    if (!node.TryAsControl(out GodotControl? control))
                    {
                        continue;
                    }

                    if (control.TryGetSize(out GodotVector2 size))
                    {
                        checksum.Add(size.X);
                        checksum.Add(size.Y);
                    }

                    if (control.TryGetGlobalPosition(out ComposedGlobalPosition global))
                    {
                        checksum.Add(global.Position.X);
                        checksum.Add(global.Position.Y);
                    }

                    if (control.TryGetVisible(out bool visible))
                    {
                        checksum.Add(visible ? 1UL : 0UL);
                    }
                }
            });
        }

        return new WorkloadResult(visited, checksum.Value, $"{polls} snapshots");
    }
}
