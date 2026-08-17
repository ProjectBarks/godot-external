using System.Globalization;
using Godot.External.Abi;
using Godot.External.Objects;
using Godot.External.Scene;

namespace Godot.External.Bench.Measurement;

/// <summary>
/// How a target's node structs are arranged in the address space — the fact the whole design hinges
/// on, measured rather than assumed.
/// </summary>
/// <param name="Nodes">Nodes reached from the root.</param>
/// <param name="DistinctPages">Distinct 4 KiB pages the node <em>bases</em> land in.</param>
/// <param name="NodesPerPage">
/// <c>Nodes / DistinctPages</c>. This is the page cache's leverage in one number: fetch 4 KiB, serve
/// this many nodes. Below about 1.5 a page cache is mostly fetching other people's data.
/// </param>
/// <param name="SamePageNeighbours">
/// Fraction of breadth-first-consecutive node pairs that share a 4 KiB page — whether the traversal
/// order and the allocation order agree, which is what decides whether a page fetched now is used
/// again soon or evicted from relevance.
/// </param>
/// <param name="MedianGap">Median absolute address distance between breadth-first-consecutive nodes.</param>
internal sealed record LocalityReport(
    int Nodes,
    int DistinctPages,
    double NodesPerPage,
    double SamePageNeighbours,
    ulong MedianGap)
{
    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Nodes} nodes across {DistinctPages} pages ({NodesPerPage:F2} nodes/4 KiB page); "
      + $"{SamePageNeighbours:P1} of BFS-consecutive pairs share a page; median gap {MedianGap:N0} B");
}

/// <summary>Measures <see cref="LocalityReport"/> from a live or replayed target.</summary>
internal static class Locality
{
    /// <summary>Walks from <paramref name="root"/> and describes where the nodes actually live.</summary>
    public static LocalityReport Measure(IByteSource source, GodotAbiProfile profile, ulong root)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);

        using SceneEpoch epoch = new(source, profile);
        TreeWalkResult walk = epoch.SceneFrom(new Bridge.NativePtr(root)).Walk();

        List<ulong> addresses = [];
        foreach (GodotNode node in walk.Nodes)
        {
            addresses.Add(node.Address.Address);
        }

        if (addresses.Count == 0)
        {
            return new LocalityReport(0, 0, 0, 0, 0);
        }

        HashSet<ulong> pages = [];
        foreach (ulong address in addresses)
        {
            pages.Add(address >> 12);
        }

        int samePage = 0;
        List<ulong> gaps = [];
        for (int i = 1; i < addresses.Count; i++)
        {
            ulong a = addresses[i - 1];
            ulong b = addresses[i];
            gaps.Add(a > b ? a - b : b - a);

            if ((a >> 12) == (b >> 12))
            {
                samePage++;
            }
        }

        gaps.Sort();

        return new LocalityReport(
            addresses.Count,
            pages.Count,
            (double)addresses.Count / pages.Count,
            gaps.Count == 0 ? 0 : (double)samePage / gaps.Count,
            gaps.Count == 0 ? 0 : gaps[gaps.Count / 2]);
    }
}
