using System.Globalization;
using Godot.External.Abi;
using Godot.External.Bench.Fixtures;
using Godot.External.Bench.Measurement;
using Godot.External.Bench.Workloads;
using Godot.External.Bridge;
#if LIVE_TARGET
using Godot.External.Bench.Targets;
#endif

namespace Godot.External.Bench;

/// <summary>
/// The benchmark entry point. With no arguments it runs the whole matrix against synthetic heaps and
/// needs nothing installed — that is the CI path.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        BenchOptions options;
        try
        {
            options = BenchOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            BenchOptions.WriteUsage(Console.Error);
            return 2;
        }

        if (options.ShowHelp)
        {
            BenchOptions.WriteUsage(Console.Out);
            return 0;
        }

        TextWriter log = Console.Out;
        List<BenchRow> rows = [];
        List<IDisposable> owned = [];

        try
        {
            IReadOnlyList<IWorkload> workloads =
            [
                new FullTreeWalkWorkload(),
                new TargetedGeometryWorkload(options.GeometryIterations),
                new SubtreePollWorkload(options.Polls),
            ];

            BenchRunner runner = new(options.Repetitions);

            log.WriteLine("# Godot.External read-path cache benchmark");
            log.WriteLine();
            log.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- host: {Environment.MachineName}, {Environment.ProcessorCount} cores, .NET {Environment.Version}"));
            log.WriteLine($"- build: {(IsOptimised() ? "Release" : "DEBUG — wall times are not meaningful")}");
            log.WriteLine($"- repetitions: {options.Repetitions} (best wall time reported)");

            foreach (BenchTarget target in BuildTargets(options, log, owned))
            {
                log.WriteLine();
                log.WriteLine($"## target: {target.Name}");
                log.WriteLine();

                LocalityReport locality = Locality.Measure(target.Source, target.Profile, target.Anchors.Root.Address);
                log.WriteLine($"- locality: {locality}");
                log.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"- node span derived from profile: {Godot.External.Memory.ObjectSpanCache.SpanBytesFor(target.Profile.Offsets, target.Profile.RealSize):N0} B "
                  + $"(with text fields: {Godot.External.Memory.ObjectSpanCache.SpanBytesFor(target.Profile.Offsets, target.Profile.RealSize, includeText: true):N0} B)"));

                foreach (IWorkload workload in workloads)
                {
                    log.WriteLine($"- workload `{workload.Name}`: {workload.Description}");
                }

                rows.AddRange(runner.Run(target, workloads, Variant.Default, log));
            }

            log.WriteLine();
            log.WriteLine("## results");
            Report.WriteMarkdown(rows, log);

            if (options.CsvPath is not null)
            {
                Report.WriteCsv(rows, options.CsvPath);
                log.WriteLine();
                log.WriteLine($"CSV written to {options.CsvPath}");
            }

            if (!options.SkipTrapDemo)
            {
                TrapDemo.Run(log);
            }

            return 0;
        }
        finally
        {
            foreach (IDisposable disposable in owned)
            {
                disposable.Dispose();
            }
        }
    }

    private static bool IsOptimised()
    {
        object[] attributes = typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), inherit: false);

        return attributes.Length == 0
            || (attributes[0] as System.Diagnostics.DebuggableAttribute)?.IsJITTrackingEnabled != true;
    }

    private static IEnumerable<BenchTarget> BuildTargets(BenchOptions options, TextWriter log, List<IDisposable> owned)
    {
        GodotAbiProfile profile = GodotAbiProfiles.Godot451Release;

        if (options.ProcessId is int pid)
        {
#if LIVE_TARGET
            log.WriteLine();
            log.WriteLine($"attaching to process {pid} (read-only)...");
            LiveTarget? live = LiveTargetLocator.Attach(pid, profile, options.AnchorNames, log);
            if (live is not null)
            {
                owned.Add(live);

                if (options.RecordPath is not null)
                {
                    yield return RecordFixture(live, options, log);
                }

                yield return new BenchTarget($"live pid {pid} ({live.NodeCount} nodes)", live.Source, live.Profile, live.Anchors);
            }
            else
            {
                log.WriteLine("  live attach failed; continuing with the other targets.");
            }
#else
            log.WriteLine();
            log.WriteLine($"--pid {pid} ignored: this build has no live target. LiveClr was not found next to "
                        + "the repository, so only the synthetic heaps and recorded fixtures are available.");
#endif
        }

        if (options.FixturePath is not null)
        {
            MemoryImage image = MemoryImage.Load(options.FixturePath, out ulong root, out ulong node, out ulong subtree);
            log.WriteLine();
            log.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"fixture {Path.GetFileName(options.FixturePath)}: {image.MappedPages:N0} pages, {image.MappedBytes / (1 << 20):N0} MiB"));

            yield return new BenchTarget(
                $"fixture {Path.GetFileNameWithoutExtension(options.FixturePath)}",
                image,
                profile,
                new WorkloadAnchors(new NativePtr(root), new NativePtr(node), new NativePtr(subtree)));
        }

        if (!options.Synthetic)
        {
            yield break;
        }

        foreach (HeapLayout layout in HeapLayout.All)
        {
            SyntheticScene scene = SyntheticSceneBuilder.Build(layout, options.SyntheticNodes, profile);
            yield return new BenchTarget(
                $"synthetic/{layout.Name}",
                scene.Memory,
                profile,
                new WorkloadAnchors(new NativePtr(scene.Root), new NativePtr(scene.DeepControl), new NativePtr(scene.SubtreeRoot)));
        }
    }

#if LIVE_TARGET
    private static BenchTarget RecordFixture(LiveTarget live, BenchOptions options, TextWriter log)
    {
        MemoryImage image = new();
        RecordingByteSource recorder = new(live.Source, image);

        // Record through the same workloads that will be replayed, uncached, so the fixture contains
        // exactly the neighbourhood every variant can ask about and nothing else.
        BenchTarget recording = new("recording", recorder, live.Profile, live.Anchors);
        BenchRunner single = new(repetitions: 1);
        single.Run(
            recording,
            [new FullTreeWalkWorkload(), new TargetedGeometryWorkload(4), new SubtreePollWorkload(2)],
            [new Variant("uncached", Godot.External.Memory.MemoryCacheOptions.None)],
            TextWriter.Null);

        image.Save(options.RecordPath!, live.Anchors.Root.Address, live.Anchors.Node.Address, live.Anchors.Subtree.Address);
        log.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  recorded {image.MappedPages:N0} pages ({image.MappedBytes / (1 << 20):N0} MiB in memory, "
          + $"{new FileInfo(options.RecordPath!).Length / (1 << 20):N1} MiB compressed) to {options.RecordPath}"));

        return new BenchTarget(
            "fixture (just recorded)",
            image,
            live.Profile,
            live.Anchors);
    }
#endif
}
