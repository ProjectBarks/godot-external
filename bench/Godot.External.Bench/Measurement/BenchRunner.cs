using System.Diagnostics;
using Godot.External.Abi;
using Godot.External.Bench.Workloads;
using Godot.External.Memory;
using Godot.External.Scene;

namespace Godot.External.Bench.Measurement;

/// <summary>One cache configuration under test, with the label it appears under in the report.</summary>
internal sealed record Variant(string Name, MemoryCacheOptions Options)
{
    /// <summary>
    /// The variant set. Page sizes are swept because the amplification factor is a direct function of
    /// page size and the sweep is what shows it; the 4 KiB row is the faithful port of LiveClr's
    /// <c>PageCache</c> and is the design this exercise set out to beat or adopt.
    /// </summary>
    public static IReadOnlyList<Variant> Default { get; } =
    [
        new("uncached", MemoryCacheOptions.None),
        new("page-128", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 128 }),
        new("page-256", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 256 }),
        new("page-512", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 512 }),
        new("page-1k", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 1024 }),
        new("page-2k", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 2048 }),
        new("page-4k", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 4096 }),
        new("page-16k", new MemoryCacheOptions { Mode = MemoryCacheMode.Page, PageSize = 16384 }),
        new("span", new MemoryCacheOptions { Mode = MemoryCacheMode.Span }),
        new("hybrid-1k", new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid, PageSize = 1024 }),
        new("hybrid-4k", new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid, PageSize = 4096 }),
        new("hybrid-4k+text", new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid, PageSize = 4096, SpanIncludesText = true }),
    ];
}

/// <summary>What one (target, workload, variant) run cost and produced.</summary>
internal sealed record BenchRow(
    string Target,
    string Workload,
    string Variant,
    long Syscalls,
    long BytesRead,
    long UsefulBytes,
    long LogicalReads,
    double HitRate,
    double WallMs,
    long SpanFetches,
    long SpanOverreads,
    long AgreeTwiceSuppressed,
    long RetainedBytes,
    int Items,
    ulong Checksum,
    string Note)
{
    /// <summary>Bytes pulled from the target per byte the library asked for.</summary>
    public double Amplification => UsefulBytes == 0 ? 0 : (double)BytesRead / UsefulBytes;
}

/// <summary>Everything a run needs about the memory it is reading.</summary>
/// <param name="Name">Label for the report.</param>
/// <param name="Source">The raw source; the counter and any cache are layered on top.</param>
/// <param name="Profile">ABI profile.</param>
/// <param name="Anchors">Where the workloads start.</param>
internal sealed record BenchTarget(string Name, IByteSource Source, GodotAbiProfile Profile, WorkloadAnchors Anchors);

/// <summary>
/// Runs every workload under every variant against a target and cross-checks that they all agree.
/// </summary>
/// <remarks>
/// The agreement check is not a formality. A cache is a correctness surface: an off-by-one in block
/// alignment, a negative entry cached from an over-read, or a stale block surviving an invalidation
/// all produce a <em>faster</em> run that returns different data. Comparing each variant's checksum
/// against the uncached run is the only thing standing between "this design is 20x cheaper" and
/// "this design is 20x cheaper and wrong".
/// </remarks>
internal sealed class BenchRunner(int repetitions = 3)
{
    /// <summary>Runs the matrix and returns one row per (workload, variant).</summary>
    public IReadOnlyList<BenchRow> Run(
        BenchTarget target,
        IReadOnlyList<IWorkload> workloads,
        IReadOnlyList<Variant> variants,
        TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(workloads);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(log);

        List<BenchRow> rows = [];

        foreach (IWorkload workload in workloads)
        {
            ulong? expected = null;
            int? expectedItems = null;

            foreach (Variant variant in variants)
            {
                BenchRow row = RunOne(target, workload, variant);

                expected ??= row.Checksum;
                expectedItems ??= row.Items;

                if (row.Checksum != expected || row.Items != expectedItems)
                {
                    log.WriteLine(
                        $"  !! {workload.Name}/{variant.Name} DISAGREES with uncached: "
                      + $"{row.Items} items / checksum {row.Checksum:X16} vs {expectedItems} / {expected:X16}");
                }

                rows.Add(row);
            }
        }

        return rows;
    }

    private BenchRow RunOne(BenchTarget target, IWorkload workload, Variant variant)
    {
        // Warm-up: JIT, and on a live target the first pass is also what faults the pages in. Both
        // belong to the harness, not to the design under test.
        Execute(target, workload, variant, out _, out _);

        double best = double.MaxValue;
        BenchContext context = null!;
        WorkloadResult result = default;

        for (int i = 0; i < repetitions; i++)
        {
            double elapsed = Execute(target, workload, variant, out context, out result);
            best = Math.Min(best, elapsed);
        }

        CacheStatistics stats = context.Total;

        // The cache's own counters and the counter underneath it are measuring the same thing from
        // opposite sides. If they disagree, one of them is lying, and the report should say so.
        string note = result.Note;
        if (variant.Options.Mode != MemoryCacheMode.None && stats.Fetches != context.Raw.Reads)
        {
            note += $" [counter mismatch: cache says {stats.Fetches} fetches, source saw {context.Raw.Reads}]";
        }

        return new BenchRow(
            target.Name,
            workload.Name,
            variant.Name,
            context.Raw.Reads,
            context.Raw.Bytes,
            stats.LogicalBytes,
            stats.LogicalReads,
            stats.HitRate,
            best,
            stats.SpanFetches,
            stats.SpanOverreads,
            stats.AgreeTwiceSuppressed,
            stats.RetainedBytes,
            result.Items,
            result.Checksum,
            note);
    }

    private static double Execute(
        BenchTarget target,
        IWorkload workload,
        Variant variant,
        out BenchContext context,
        out WorkloadResult result)
    {
        MeasuredByteSource measured = new(target.Source);
        using SceneEpoch epoch = new SceneEpochFactory(measured, target.Profile).Create();
        context = new BenchContext(epoch, variant.Options, measured);

        Stopwatch clock = Stopwatch.StartNew();
        result = workload.Run(context, target.Anchors);
        clock.Stop();

        return clock.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Builds an epoch over an arbitrary <see cref="IByteSource"/>. The public
    /// <see cref="SceneEpoch.Begin"/> takes a delegate, which would put an extra frame between the
    /// measurement and the target.
    /// </summary>
    private sealed class SceneEpochFactory(IByteSource source, GodotAbiProfile profile)
    {
        public SceneEpoch Create() => new(source, profile);
    }
}
