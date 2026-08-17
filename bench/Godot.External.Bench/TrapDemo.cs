using System.Buffers.Binary;
using Godot.External.Abi;
using Godot.External.Bench.Fixtures;
using Godot.External.Bridge;
using Godot.External.Memory;
using Godot.External.Objects;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Bench;

/// <summary>
/// Demonstrates, on a target that is deliberately changing under the reader, exactly what the
/// caching layer hides and exactly what it does not.
/// </summary>
/// <remarks>
/// This runs as part of the benchmark rather than as a test because it is evidence, not a
/// regression guard: the numbers it prints are the argument for the invalidation design, and the
/// last section is the argument against trusting it too far.
/// </remarks>
internal static class TrapDemo
{
    /// <summary>Runs all four demonstrations against a small mutating synthetic scene.</summary>
    public static void Run(TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(log);

        GodotAbiProfile profile = GodotAbiProfiles.Godot451Release;
        SyntheticScene scene = SyntheticSceneBuilder.Build(HeapLayout.Clustered, nodeCount: 200, profile);

        // A node whose size changes on every single read of it. Nothing in a real game moves this
        // fast; the point is to make "did it change between the two readings?" have an unambiguous
        // right answer.
        ulong ticking = scene.DeepControl + (ulong)profile.Offsets.ControlSize;
        MutatingByteSource mutating = new(scene.Memory, ticking);

        log.WriteLine();
        log.WriteLine("## the invalidation trap");
        log.WriteLine();
        log.WriteLine("A synthetic target whose `size_cache` changes on **every read**, so any check that");
        log.WriteLine("works by reading twice must see a difference — unless something is serving the second");
        log.WriteLine("read from the first read's bytes.");
        log.WriteLine();

        AgreeTwice(log, mutating, profile, scene);
        TwoReadings(log, mutating, profile, scene);
        SnapshotPerPoll(log, mutating, profile, scene);
        HeldSnapshot(log, mutating, profile, scene);
    }

    private static void AgreeTwice(TextWriter log, IByteSource source, GodotAbiProfile profile, SyntheticScene scene)
    {
        long uncachedReads = CountWalkReads(source, profile, scene.Root, MemoryCacheOptions.None);
        long cachedReads = CountWalkReads(source, profile, scene.Root, new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid });

        using SceneEpoch epoch = new(source, profile);
        using MemorySnapshot snapshot = epoch.Snapshot(new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid });
        epoch.SceneFrom(new NativePtr(scene.Root)).Walk();

        log.WriteLine("### 1. agree-twice, inside a coherent snapshot (handled)");
        log.WriteLine();
        log.WriteLine($"- uncached walk: {uncachedReads:N0} logical reads, both traversals performed");
        log.WriteLine($"- snapshotted walk: {cachedReads:N0} logical reads");
        log.WriteLine($"- `AgreeTwiceSuppressed` = {snapshot.Statistics.AgreeTwiceSuppressed:N0}");
        log.WriteLine();
        log.WriteLine("The second traversal would have read the same frozen bytes and could only have agreed.");
        log.WriteLine("`ChildListWalk.WalkStable` asks `IsCoherent` and declines to run it, so the mitigation is");
        log.WriteLine("*replaced* by the stronger one rather than silently cancelled by it (§6.4).");
        log.WriteLine();
    }

    private static void TwoReadings(TextWriter log, IByteSource source, GodotAbiProfile profile, SyntheticScene scene)
    {
        log.WriteLine("### 2. a hand-written \"read it twice\" check (detected, not prevented)");
        log.WriteLine();

        (bool uncachedAgreed, long _) = ReadTwice(source, profile, scene, MemoryCacheOptions.None);
        (bool cachedAgreed, long repeats) = ReadTwice(
            source,
            profile,
            scene,
            new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid, DetectRepeatedReads = true });

        log.WriteLine($"- uncached: the two readings agreed? **{uncachedAgreed}** (correct — the value is changing)");
        log.WriteLine($"- snapshotted: the two readings agreed? **{cachedAgreed}** (the check has been defeated)");
        log.WriteLine($"- `RepeatedReads` = {repeats:N0} with `DetectRepeatedReads` on");
        log.WriteLine();
        log.WriteLine("This is the calibrator's bug reproduced deliberately. The library cannot know that a");
        log.WriteLine("caller's second read was *meant* to observe change, so it counts them instead: a non-zero");
        log.WriteLine("`RepeatedReads` says \"you read some address twice and got the same answer by construction\".");
        log.WriteLine();
    }

    private static void SnapshotPerPoll(TextWriter log, IByteSource source, GodotAbiProfile profile, SyntheticScene scene)
    {
        using SceneEpoch epoch = new(source, profile);
        List<double> sizes = [];

        for (int poll = 0; poll < 3; poll++)
        {
            using MemorySnapshot snapshot = epoch.Snapshot(new MemoryCacheOptions { Mode = MemoryCacheMode.Hybrid });
            GodotControl control = epoch.ControlUnchecked(new NativePtr(scene.DeepControl));
            if (control.TryGetSize(out GodotVector2 size))
            {
                sizes.Add(size.X);
            }
        }

        log.WriteLine("### 3. a snapshot per poll (correct usage)");
        log.WriteLine();
        log.WriteLine($"- three polls, three snapshots: sizes {string.Join(", ", sizes)}");
        log.WriteLine($"- distinct values: {sizes.Distinct().Count()} of {sizes.Count} — each poll observed its own moment");
        log.WriteLine();
    }

    private static void HeldSnapshot(TextWriter log, IByteSource source, GodotAbiProfile profile, SyntheticScene scene)
    {
        using SceneEpoch epoch = new(source, profile);
        using MemorySnapshot snapshot = epoch.Snapshot(new MemoryCacheOptions
        {
            Mode = MemoryCacheMode.Hybrid,
            MaxAge = TimeSpan.Zero,
            DetectRepeatedReads = true,
        });

        List<double> sizes = [];
        for (int poll = 0; poll < 3; poll++)
        {
            GodotControl control = epoch.ControlUnchecked(new NativePtr(scene.DeepControl));
            if (control.TryGetSize(out GodotVector2 size))
            {
                sizes.Add(size.X);
            }
        }

        bool refused;
        try
        {
            using MemorySnapshot second = epoch.Snapshot();
            refused = false;
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        log.WriteLine("### 4. one snapshot held across polls (**the misuse that survives**)");
        log.WriteLine();
        log.WriteLine($"- three polls, one snapshot: sizes {string.Join(", ", sizes)}");
        log.WriteLine($"- distinct values: {sizes.Distinct().Count()} of {sizes.Count} — polls two and three saw poll one");
        log.WriteLine($"- `IsStale` = {snapshot.IsStale}, `StaleReads` = {snapshot.Statistics.StaleReads:N0}, "
                    + $"`RepeatedReads` = {snapshot.Statistics.RepeatedReads:N0}, age {snapshot.Age.TotalMilliseconds:F1} ms");
        log.WriteLine($"- opening a second snapshot while this one is live was refused: **{refused}**");
        log.WriteLine();
        log.WriteLine("Nothing here prevents this. A caller who never opens a second snapshot and never looks at");
        log.WriteLine("`IsStale` gets the first poll's data forever, silently. The scope, the one-at-a-time rule");
        log.WriteLine("and the counters make it awkward and observable; they do not make it impossible.");
        log.WriteLine();
    }

    private static (bool Agreed, long Repeats) ReadTwice(
        IByteSource source,
        GodotAbiProfile profile,
        SyntheticScene scene,
        MemoryCacheOptions options)
    {
        using SceneEpoch epoch = new(source, profile);
        using MemorySnapshot snapshot = epoch.Snapshot(options);

        GodotControl control = epoch.ControlUnchecked(new NativePtr(scene.DeepControl));
        control.TryGetSize(out GodotVector2 first);
        control.TryGetSize(out GodotVector2 second);

        return (first.X == second.X, snapshot.Statistics.RepeatedReads);
    }

    private static long CountWalkReads(IByteSource source, GodotAbiProfile profile, ulong root, MemoryCacheOptions options)
    {
        using SceneEpoch epoch = new(source, profile);
        using MemorySnapshot snapshot = epoch.Snapshot(options);
        epoch.SceneFrom(new NativePtr(root)).Walk();
        return snapshot.Statistics.LogicalReads;
    }

    /// <summary>A memory image in which one four-byte field advances on every read of the image.</summary>
    private sealed class MutatingByteSource(MemoryImage image, ulong tickingAddress) : IByteSource
    {
        private float _value = 100;

        /// <inheritdoc/>
        public bool Is64Bit => true;

        /// <inheritdoc/>
        public bool TryRead(ulong address, Span<byte> buffer)
        {
            _value += 1;

            Span<byte> encoded = stackalloc byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(encoded, _value);
            image.Write(tickingAddress, encoded);

            return image.TryRead(address, buffer);
        }
    }
}
