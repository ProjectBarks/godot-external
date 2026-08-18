using System.Globalization;
using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;
using Godot.External.Calibrator.Reflection;
using Godot.External.Calibrator.Target;
using LiveClr.Memory;

namespace Godot.External.Calibrator;

/// <summary>
/// The <c>godot-abi-grid/driver.v1</c> entry point: one JSON request on stdin, one JSON result on
/// stdout, exit 0.
/// </summary>
/// <remarks>
/// <para>
/// The exit code carries meaning the harness relies on. A non-zero exit is recorded as
/// <c>error</c> — "the driver crashed, this is not a calibrator verdict" — and is reserved here for
/// exactly that: no pid, or a process that will not open. Everything else, including "no offset was
/// derivable on this build", exits 0 with a result, because that <em>is</em> a verdict and hiding it
/// behind an error would quietly remove a red cell from the matrix.
/// </para>
/// <para>
/// <c>GRID_ROOT_PTR</c> short-circuits the whole-process scan with a known walk-root address. It
/// exists for reproducing a single cell by hand; the harness never sets it, and a run that used it
/// says so in its notes.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>Environment variable holding a hex walk-root <c>Node*</c>, if the operator has one.</summary>
    public const string RootPointerEnvironmentVariable = "GRID_ROOT_PTR";

    /// <summary>Runs one calibration.</summary>
    public static int Main()
    {
        // Some shells prepend a UTF-8 BOM when piping; JsonDocument treats it as a stray byte.
        string input = Console.In.ReadToEnd().TrimStart('﻿');

        DriverRequest request;
        try
        {
            request = DriverRequest.Parse(input);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"unparseable {DriverRequest.ContractId} request: {ex.Message}");
            return 2;
        }

        if (request.Pid is not int pid || pid <= 0)
        {
            Console.Error.WriteLine(
                "request carried no pid. This driver reads a live target out of process; there is nothing "
                + "to calibrate against without one.");
            return 2;
        }

        if (!WindowsProcessMemory.TryOpen(pid, out WindowsProcessMemory? process))
        {
            Console.Error.WriteLine($"could not open pid {pid} for reading (PROCESS_VM_READ | PROCESS_QUERY_INFORMATION).");
            return 2;
        }

        List<string> notes = [];
        DriverResult result;

        using (process)
        using (PageCache cache = new(process))
        {
            ClrManagedProbe? managed = request.IsDotNetCell ? ClrManagedProbe.TryAttach(pid, notes) : null;
            try
            {
                // The cache is what makes the scan affordable, and it is also what would make every
                // "read it again" check a lie: a repeat read answered from the pages already held
                // cannot tell a stable field from per-frame state. Clearing it is the whole point.
                result = Calibrate(cache, process, request, managed, notes, cache.Clear);
            }
            finally
            {
                managed?.Dispose();
            }
        }

        // dotnet run prints build chatter ahead of us; lib/driver.mjs recovers the object by looking
        // for a brace at the start of a line, so give it one.
        Console.Out.Write('\n');
        Console.Out.Write(result.ToJson());
        Console.Out.Write('\n');
        Console.Out.Flush();
        return 0;
    }

    private static DriverResult Calibrate(
        IMemoryReader reader,
        WindowsProcessMemory process,
        DriverRequest request,
        IManagedProbe? managed,
        List<string> notes,
        Action refresh)
    {
        ulong? root = ExplicitRoot(notes);
        RegionScanner scanner = new(reader, new ProcessRegionSource(process));

        if (root is null)
        {
            RootLocation? located = new RootLocator(reader, scanner).Locate(
                request.WalkRootName ?? string.Empty,
                request.KnownRootChildNames(),
                request.Names,
                request.NodeCount ?? 0,
                notes);

            if (located is not null)
            {
                root = located.Root;
                notes.Add(
                    $"walk root 0x{located.Root:x} located by {located.How}; the same solve gave "
                    + $"node.name 0x{located.Anchor.NameOffset:x} and node.parent 0x{located.Anchor.ParentOffset:x} "
                    + "before either was derived again from the walk.");
            }

            if (scanner.LastScanTruncated)
            {
                notes.Add("a whole-process scan hit its byte budget and stopped early; anything not found may "
                        + "simply not have been reached.");
            }
        }

        if (root is null)
        {
            return new DriverResult
            {
                EngineVersion = request.EngineVersion,
                Notes = [.. notes, "no walk root could be located, so nothing was derived. This is a calibration "
                                 + "verdict, not a driver error."],
            };
        }

        ClassDbCorroborator? corroborator = BuildCorroborator(reader, process, scanner, request, notes);
        DriverResult result = new CalibrationSession(reader, request, managed, refresh, corroborator).Run(root.Value);
        return result with { Notes = [.. notes, .. result.Notes] };
    }

    /// <summary>
    /// Builds the independent getter-disassembly route, or explains why there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is assembled <em>here</em> rather than inside the session because the two things it needs —
    /// a whole-process scanner and the engine executable's base address — are process facts the
    /// session is deliberately not given. The session receives a finished collaborator and a set of
    /// derived offsets, which is what keeps the second route from being able to see anything the first
    /// one did beyond its published answers.
    /// </para>
    /// <para>
    /// The first module in load order is the main image. Reading its base from the loader rather than
    /// scanning for an MZ header matters: several mapped images start with one.
    /// </para>
    /// <para>
    /// It reads through the same <c>PageCache</c> the rest of the run uses. The route makes on the
    /// order of a hundred thousand small reads chasing chains, and <c>ClassDB</c> registration is
    /// finished long before a scene exists — so a cached page is not a stale reading here, it is the
    /// same immutable table read twice.
    /// </para>
    /// </remarks>
    private static ClassDbCorroborator? BuildCorroborator(
        IMemoryReader reader,
        WindowsProcessMemory process,
        RegionScanner scanner,
        DriverRequest request,
        ICollection<string> notes)
    {
        ModuleInfo? main = process.Modules.Modules.Count > 0 ? process.Modules.Modules[0] : null;

        if (main is null || main.BaseAddress == 0)
        {
            notes.Add("no main module could be enumerated, so the getter-disassembly cross-check cannot tell a "
                    + "code address from a heap pointer and is not run.");
            return null;
        }

        return new ClassDbCorroborator(reader, scanner, main.BaseAddress, request.EngineVersion);
    }

    private static ulong? ExplicitRoot(ICollection<string> notes)
    {
        string? text = Environment.GetEnvironmentVariable(RootPointerEnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        string digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (!ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value) || value == 0)
        {
            notes.Add($"{RootPointerEnvironmentVariable}=\"{text}\" is not a hex address; ignoring it.");
            return null;
        }

        notes.Add($"walk root 0x{value:x} taken from {RootPointerEnvironmentVariable}; it was NOT discovered by "
                + "this run.");
        return value;
    }
}
