using Godot.External.Memory;
using Godot.External.Scene;

namespace Godot.External.Bench.Measurement;

/// <summary>
/// One variant's run: the epoch under test, the cache configuration, and the accumulated cost of
/// every snapshot the workload opened.
/// </summary>
/// <remarks>
/// Snapshots are accumulated rather than assumed to be one, because the number of them is part of
/// what is being measured. The polling workload deliberately opens a fresh snapshot per poll — that
/// is the correct granularity and the reason its hit rate is lower than the tree walk's.
/// </remarks>
internal sealed class BenchContext(SceneEpoch epoch, MemoryCacheOptions options, MeasuredByteSource raw)
{
    private CacheStatistics _total;

    /// <summary>The epoch under test.</summary>
    public SceneEpoch Epoch { get; } = epoch ?? throw new ArgumentNullException(nameof(epoch));

    /// <summary>Cache configuration every snapshot is opened with.</summary>
    public MemoryCacheOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>The counter sitting between the cache and the target.</summary>
    public MeasuredByteSource Raw { get; } = raw ?? throw new ArgumentNullException(nameof(raw));

    /// <summary>Snapshots opened.</summary>
    public int Snapshots { get; private set; }

    /// <summary>Summed statistics across every snapshot this run opened.</summary>
    public CacheStatistics Total => _total;

    /// <summary>
    /// Runs <paramref name="body"/> inside a snapshot, accumulating its cost. The snapshot is closed
    /// on the way out whatever happens — which is the property the whole design rests on.
    /// </summary>
    public void InSnapshot(Action<MemorySnapshot> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        using MemorySnapshot snapshot = Epoch.Snapshot(Options);
        try
        {
            body(snapshot);
        }
        finally
        {
            Accumulate(snapshot.Statistics);
            Snapshots++;
        }
    }

    private void Accumulate(CacheStatistics stats) => _total = new CacheStatistics
    {
        LogicalReads = _total.LogicalReads + stats.LogicalReads,
        LogicalBytes = _total.LogicalBytes + stats.LogicalBytes,
        Hits = _total.Hits + stats.Hits,
        Misses = _total.Misses + stats.Misses,
        Fetches = _total.Fetches + stats.Fetches,
        FetchedBytes = _total.FetchedBytes + stats.FetchedBytes,
        SpanFetches = _total.SpanFetches + stats.SpanFetches,
        SpanOverreads = _total.SpanOverreads + stats.SpanOverreads,
        BlockFetches = _total.BlockFetches + stats.BlockFetches,
        NegativeEntries = _total.NegativeEntries + stats.NegativeEntries,
        AgreeTwiceSuppressed = _total.AgreeTwiceSuppressed + stats.AgreeTwiceSuppressed,
        RepeatedReads = _total.RepeatedReads + stats.RepeatedReads,
        StaleReads = _total.StaleReads + stats.StaleReads,

        // Peak, not sum: this is the memory a caller must be prepared to hold at one moment, and
        // summing it across snapshots would describe a cache nobody ever built.
        RetainedEntries = Math.Max(_total.RetainedEntries, stats.RetainedEntries),
        RetainedBytes = Math.Max(_total.RetainedBytes, stats.RetainedBytes),
    };
}
