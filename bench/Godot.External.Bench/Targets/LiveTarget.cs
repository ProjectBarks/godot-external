using System.Buffers.Binary;
using Godot.External.Abi;
using Godot.External.Bench.Workloads;
using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Scene;
using Godot.External.Values;
using LiveClr.Memory;

namespace Godot.External.Bench.Targets;

/// <summary>Adapts LiveClr's reader to this library's seam. The one-file join docs/analysis.md §8.8 describes.</summary>
internal sealed class MemoryReaderByteSource(IMemoryReader reader) : IByteSource
{
    private readonly IMemoryReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <inheritdoc/>
    public bool Is64Bit => _reader.Is64Bit;

    /// <inheritdoc/>
    public bool TryRead(ulong address, Span<byte> buffer) => _reader.TryRead(address, buffer);
}

/// <summary>An attached game, with its scene root located and workload anchors chosen.</summary>
/// <param name="Memory">The open process handle. <b>Read-only; never written, suspended or modified.</b></param>
/// <param name="Source">The raw byte source over it.</param>
/// <param name="Profile">ABI profile in force.</param>
/// <param name="Anchors">Where the workloads start.</param>
/// <param name="NodeCount">Nodes the located root reaches.</param>
/// <param name="How">How the root was found, for the report.</param>
internal sealed record LiveTarget(
    WindowsProcessMemory Memory,
    IByteSource Source,
    GodotAbiProfile Profile,
    WorkloadAnchors Anchors,
    int NodeCount,
    string How) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Memory.Dispose();
}

/// <summary>
/// Attaches to a running Godot game and finds a walk root, so the benchmark can measure the access
/// pattern it exists to measure rather than a model of it.
/// </summary>
/// <remarks>
/// <para>
/// The route is the cheap half of the calibrator's root locator: scan for the UTF-32 characters of a
/// node name, follow pointer identity back to <c>node + NodeName</c>, then climb the parent chain to
/// whatever has no parent. The full locator solves the offsets as well; here the offsets are already
/// known — the shipped release profile was validated against this exact game — so only the address
/// is missing.
/// </para>
/// <para>
/// <b>Read-only throughout.</b> The process is opened for query and read, and nothing in this file
/// writes, injects, suspends, or terminates anything.
/// </para>
/// </remarks>
internal static class LiveTargetLocator
{
    /// <summary>Names tried, in order, when the caller supplies none.</summary>
    /// <remarks>
    /// <c>root</c> is Godot's own name for the <c>SceneTree</c>'s root <c>Window</c> and so exists in
    /// every Godot game; the rest are nodes docs/analysis.md §12.3/§12.3b observed live in Slay the
    /// Spire 2, several of which are autoloads and therefore present whatever the game is showing.
    /// </remarks>
    public static IReadOnlyList<string> DefaultAnchorNames { get; } =
    [
        "AudioManager",
        "FmodBankLoader",
        "MainMenuTextButtons",
        "BgContainer",
        "Proxy",
        "root",
    ];

    /// <summary>Attaches and locates. Returns null with an explanation rather than throwing.</summary>
    public static LiveTarget? Attach(
        int processId,
        GodotAbiProfile profile,
        IReadOnlyList<string>? anchorNames,
        TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(log);

        if (!WindowsProcessMemory.TryOpen(processId, out WindowsProcessMemory? memory))
        {
            log.WriteLine($"  could not open process {processId} for reading.");
            return null;
        }

        IByteSource source = new MemoryReaderByteSource(memory);
        ProcessScanner scanner = new(memory);

        foreach (string name in anchorNames ?? DefaultAnchorNames)
        {
            log.WriteLine($"  scanning for node name \"{name}\"...");
            foreach (ulong node in NodeCandidates(scanner, source, profile, name))
            {
                if (!TryBuildTarget(memory, source, profile, node, name, log, out LiveTarget? target))
                {
                    continue;
                }

                return target;
            }
        }

        log.WriteLine("  no anchor name resolved to a walkable scene tree.");
        memory.Dispose();
        return null;
    }

    private static bool TryBuildTarget(
        WindowsProcessMemory memory,
        IByteSource source,
        GodotAbiProfile profile,
        ulong node,
        string anchorName,
        TextWriter log,
        out LiveTarget? target)
    {
        target = null;

        ulong root = ClimbToRoot(source, profile, node);

        using SceneEpoch probe = new(source, profile);
        TreeWalkResult walk = probe.SceneFrom(new NativePtr(root)).Walk();

        // A coincidence in the scan produces a "node" with one or two plausible neighbours. A scene
        // does not; requiring a real tree is what separates the two without needing a harness.
        if (walk.Nodes.Count < 64)
        {
            return false;
        }

        if (!TryChooseAnchors(probe, walk, root, out WorkloadAnchors? anchors) || anchors is null)
        {
            return false;
        }

        log.WriteLine(
            $"  root 0x{root:X} reaches {walk.Nodes.Count} nodes (status {walk.WorstStatus}), "
          + $"anchored on \"{anchorName}\" at 0x{node:X}.");

        target = new LiveTarget(
            memory,
            source,
            profile,
            anchors,
            walk.Nodes.Count,
            $"UTF-32 scan for \"{anchorName}\", then parent chain to the root");

        return true;
    }

    private static bool TryChooseAnchors(
        SceneEpoch epoch,
        TreeWalkResult walk,
        ulong root,
        out WorkloadAnchors? anchors)
    {
        anchors = null;

        // The deepest Control gives the targeted-read workload a real ancestor chain to compose up,
        // which is the only part of that workload with more than one object in it.
        GodotNode? deepest = null;
        int deepestDepth = -1;

        foreach (GodotNode node in walk.Nodes)
        {
            if (!node.IsControl)
            {
                continue;
            }

            int depth = node.Ancestors().Count();
            if (depth > deepestDepth)
            {
                deepestDepth = depth;
                deepest = node;
            }
        }

        if (deepest is null)
        {
            return false;
        }

        // A few levels up from it: an overlay-sized cluster, not the whole tree.
        GodotNode subtree = deepest;
        for (int i = 0; i < 3; i++)
        {
            if (!subtree.TryGetParent(out GodotNode? parent) || parent is null)
            {
                break;
            }

            subtree = parent;
        }

        anchors = new WorkloadAnchors(new NativePtr(root), deepest.Address, subtree.Address);
        return true;
    }

    private static ulong ClimbToRoot(IByteSource source, GodotAbiProfile profile, ulong node)
    {
        HashSet<ulong> seen = [node];
        ulong current = node;

        for (int depth = 0; depth < ControlGeometry.MaxAncestorDepth; depth++)
        {
            if (!source.TryReadPointer(profile.FieldAddress(current, GodotField.NodeParent), out ulong parent)
                || parent == 0
                || !seen.Add(parent))
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    /// <summary>
    /// Node addresses whose <c>Node::name</c> decodes to <paramref name="name"/>, found by identity
    /// rather than by guessing: characters, then the slot pointing at them, then the slot pointing at
    /// that.
    /// </summary>
    private static IEnumerable<ulong> NodeCandidates(
        ProcessScanner scanner,
        IByteSource source,
        GodotAbiProfile profile,
        string name)
    {
        const int Limit = 256;

        IReadOnlyList<ulong> buffers = scanner.FindBytes(Utf32Needle(name), Limit);
        if (buffers.Count == 0)
        {
            yield break;
        }

        IReadOnlyList<ulong> bufferSlots = scanner.FindPointersTo(buffers, Limit);
        if (bufferSlots.Count == 0)
        {
            yield break;
        }

        // A slot pointing at the character buffer is StringName::_Data + StringNameDataToBuffer.
        HashSet<ulong> dataCandidates = [];
        foreach (ulong slot in bufferSlots)
        {
            if (slot >= (ulong)profile.Offsets.StringNameDataToBuffer)
            {
                dataCandidates.Add(slot - (ulong)profile.Offsets.StringNameDataToBuffer);
            }
        }

        if (dataCandidates.Count == 0)
        {
            yield break;
        }

        foreach (ulong slot in scanner.FindPointersTo(dataCandidates, Limit))
        {
            if (slot < (ulong)profile.Offsets.NodeName)
            {
                continue;
            }

            ulong node = slot - (ulong)profile.Offsets.NodeName;

            // Read the name back through the library's own decoder. A slot that only looked like a
            // name field fails here.
            if (GodotStringName.TryReadNodeName(source, profile, node, out string decoded)
                && string.Equals(decoded, name, StringComparison.Ordinal))
            {
                yield return node;
            }
        }
    }

    private static byte[] Utf32Needle(string value)
    {
        byte[] needle = new byte[value.Length * 4];
        for (int i = 0; i < value.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(needle.AsSpan(i * 4), value[i]);
        }

        return needle;
    }
}
