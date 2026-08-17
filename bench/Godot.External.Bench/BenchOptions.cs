using System.Globalization;

namespace Godot.External.Bench;

/// <summary>Command line. Every option has a default that makes <c>dotnet run</c> alone do something useful.</summary>
internal sealed record BenchOptions
{
    /// <summary>Attach to this process and measure it live.</summary>
    public int? ProcessId { get; init; }

    /// <summary>Record the live target's memory to this fixture and measure the fixture too.</summary>
    public string? RecordPath { get; init; }

    /// <summary>Replay this fixture.</summary>
    public string? FixturePath { get; init; }

    /// <summary>Write every column here.</summary>
    public string? CsvPath { get; init; }

    /// <summary>Run the synthetic heaps. On by default; the only target that needs nothing installed.</summary>
    public bool Synthetic { get; init; } = true;

    /// <summary>Nodes in each synthetic tree. The default is the live tree's peak (§12.4e).</summary>
    public int SyntheticNodes { get; init; } = 2341;

    /// <summary>Iterations for the targeted-read workload.</summary>
    public int GeometryIterations { get; init; } = 200;

    /// <summary>Polls for the polling workload.</summary>
    public int Polls { get; init; } = 20;

    /// <summary>Measured repetitions; the best wall time is reported.</summary>
    public int Repetitions { get; init; } = 3;

    /// <summary>Node names to anchor the live scan on. Null means the built-in list.</summary>
    public IReadOnlyList<string>? AnchorNames { get; init; }

    /// <summary>Skip the invalidation-trap demonstration at the end.</summary>
    public bool SkipTrapDemo { get; init; }

    /// <summary>Print usage and exit.</summary>
    public bool ShowHelp { get; init; }

    /// <summary>Parses <paramref name="args"/>.</summary>
    /// <exception cref="ArgumentException">An argument was unrecognised or malformed.</exception>
    public static BenchOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        BenchOptions options = new();
        List<string> anchors = [];

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    options = options with { ShowHelp = true };
                    break;
                case "--pid":
                    options = options with { ProcessId = int.Parse(Next(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--record":
                    options = options with { RecordPath = Next(args, ref i, arg) };
                    break;
                case "--fixture":
                    options = options with { FixturePath = Next(args, ref i, arg) };
                    break;
                case "--csv":
                    options = options with { CsvPath = Next(args, ref i, arg) };
                    break;
                case "--anchor":
                    anchors.Add(Next(args, ref i, arg));
                    break;
                case "--nodes":
                    options = options with { SyntheticNodes = int.Parse(Next(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--polls":
                    options = options with { Polls = int.Parse(Next(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--geometry-iterations":
                    options = options with { GeometryIterations = int.Parse(Next(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--repetitions":
                    options = options with { Repetitions = int.Parse(Next(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--no-synthetic":
                    options = options with { Synthetic = false };
                    break;
                case "--no-trap-demo":
                    options = options with { SkipTrapDemo = true };
                    break;
                default:
                    throw new ArgumentException($"Unrecognised argument: {arg}");
            }
        }

        return anchors.Count > 0 ? options with { AnchorNames = anchors } : options;
    }

    /// <summary>Prints the option list.</summary>
    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("""
            Godot.External read-path cache benchmark.

              dotnet run -c Release --project bench/Godot.External.Bench

            With no arguments: three synthetic heaps (sequential, clustered, scattered) x three
            workloads x every cache variant. Needs no game and no fixture — this is the CI path.

              --pid <n>               also attach to a running Godot game (READ ONLY) and measure it
              --anchor <name>         node name to anchor the live scan on; repeatable
              --record <path>         record the live target to a replayable fixture
              --fixture <path>        replay a recorded fixture
              --csv <path>            write every column as CSV
              --nodes <n>             synthetic tree size (default 2341, the live peak)
              --polls <n>             polls in the polling workload (default 20)
              --geometry-iterations <n>
              --repetitions <n>       measured repetitions; best wall time wins (default 3)
              --no-synthetic          skip the synthetic heaps
              --no-trap-demo          skip the invalidation-trap demonstration
            """);
    }

    private static string Next(string[] args, ref int i, string option)
        => ++i < args.Length ? args[i] : throw new ArgumentException($"{option} needs a value.");
}
