using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Memory;
using Godot.External.Objects;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// The caching layer: that it is off by default, that it freezes what it has read, that it does not
/// invent unreadable memory, and that it refuses to let a temporal check pretend.
/// </summary>
/// <remarks>
/// docs/analysis.md §6.4 and the calibrator's two-readings defect are both "a cache silently served
/// a read that was supposed to observe change". The tests below pin the mechanisms that answer that:
/// <see cref="SceneEpoch.Snapshot"/> being opt-in and scoped,
/// <see cref="CacheStatistics.AgreeTwiceSuppressed"/> being incremented rather than the check being
/// quietly neutralised, and <see cref="CacheStatistics.RepeatedReads"/> counting the case the library
/// cannot decide for the caller.
/// </remarks>
public class MemoryCacheTests
{
    private const ulong Base = 0x0000_0200_0000_0000UL;

    [Fact]
    public void EpochWithNoSnapshot_ReadsTheTargetEveryTime()
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Game");

        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));

        Assert.True(node.TryGetName(out _));
        int after = scene.Source.ReadCount;
        Assert.True(node.TryGetName(out _));

        // Every read still costs a read. Caching is opt-in, and constructing a handle must not have
        // quietly turned it on.
        Assert.True(scene.Source.ReadCount > after);
    }

    [Fact]
    public void PageCache_FetchesEachBlockOnce_AndServesTheRestFromIt()
    {
        PagedSource source = new();
        source.MapPage(Base);
        source.WriteUInt64(Base + 0x10, 0xAAAA);
        source.WriteUInt64(Base + 0x800, 0xBBBB);

        using SnapshotPageCache cache = new(source, pageSize: 4096);

        Assert.True(cache.TryReadUInt64(Base + 0x10, out ulong first));
        Assert.True(cache.TryReadUInt64(Base + 0x800, out ulong second));

        Assert.Equal(0xAAAAUL, first);
        Assert.Equal(0xBBBBUL, second);
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, cache.Statistics.Fetches);
        Assert.Equal(4096, cache.Statistics.FetchedBytes);
    }

    [Fact]
    public void PageCache_KeepsAFailedBlockFailed_EvenAfterItBecomesReadable()
    {
        PagedSource source = new();

        using SnapshotPageCache cache = new(source, pageSize: 4096);
        Assert.False(cache.TryReadUInt64(Base, out _));

        // A page appearing mid-traversal is exactly the mixed-moment image the cache exists to
        // prevent, so the negative entry is replayed rather than re-probed.
        source.MapPage(Base);
        source.WriteUInt64(Base, 0x1234);

        Assert.False(cache.TryReadUInt64(Base, out _));
        Assert.Equal(1, cache.Statistics.NegativeEntries);
    }

    [Fact]
    public void SpanCache_ServesAWholeNodeFromOneFetch()
    {
        PagedSource source = new();
        source.MapPage(Base);
        source.MapPage(Base + 4096);
        source.WriteUInt64(Base + 0x128, 0x11);
        source.WriteUInt64(Base + 0x148, 0x22);
        source.WriteUInt64(Base + 0x1c0, 0x33);

        int span = ObjectSpanCache.SpanBytesFor(GodotAbiProfiles.Godot451Release.Offsets, realSize: 4);
        using ObjectSpanCache cache = new(source, MemoryCacheMode.Span, span);
        cache.RegisterObject(Base);

        Assert.True(cache.TryReadUInt64(Base + 0x128, out ulong parent));
        Assert.True(cache.TryReadUInt64(Base + 0x148, out ulong children));
        Assert.True(cache.TryReadUInt64(Base + 0x1c0, out ulong name));

        Assert.Equal(0x11UL, parent);
        Assert.Equal(0x22UL, children);
        Assert.Equal(0x33UL, name);
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, cache.Statistics.SpanFetches);
    }

    [Fact]
    public void SpanBytesFor_CoversSizeCache_AndOnlyCoversTextWhenAsked()
    {
        GodotOffsetTable offsets = GodotAbiProfiles.Godot451Release.Offsets;

        // size_cache at 0x4c0 is the highest node-relative field short of the text pointers.
        Assert.Equal(0x4c8, ObjectSpanCache.SpanBytesFor(offsets, realSize: 4));

        // RichTextLabel::text at 0xa78 is the highest of all.
        Assert.Equal(0xa80, ObjectSpanCache.SpanBytesFor(offsets, realSize: 4, includeText: true));
    }

    [Fact]
    public void SpanOverread_PastTheEndOfAMapping_DoesNotInventUnreadableMemory()
    {
        // One page only: a node near its end has a span that runs off the end of the mapping. The
        // over-read fails; the fields the caller actually wants are present and must still be served.
        PagedSource source = new();
        source.MapPage(Base);
        ulong node = Base + 4096 - 0x200;
        source.WriteUInt64(node + 0x128, 0xFEED);

        int span = ObjectSpanCache.SpanBytesFor(GodotAbiProfiles.Godot451Release.Offsets, realSize: 4);
        using ObjectSpanCache cache = new(source, MemoryCacheMode.Span, span);
        cache.RegisterObject(node);

        Assert.True(cache.TryReadUInt64(node + 0x128, out ulong value));
        Assert.Equal(0xFEEDUL, value);
        Assert.Equal(1, cache.Statistics.SpanOverreads);
        Assert.Equal(0, cache.Statistics.NegativeEntries);
    }

    [Theory]
    [InlineData(MemoryCacheMode.Page)]
    [InlineData(MemoryCacheMode.Span)]
    [InlineData(MemoryCacheMode.Hybrid)]
    public void Snapshot_FreezesTheImage_AndReleasesItOnDispose(MemoryCacheMode mode)
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Before");
        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));

        using (epoch.Snapshot(new MemoryCacheOptions { Mode = mode }))
        {
            Assert.True(node.TryGetName(out string first));
            Assert.Equal("Before", first);

            scene.SetName(root, "After");

            Assert.True(node.TryGetName(out string second));
            Assert.Equal("Before", second); // frozen, which is the point
        }

        Assert.True(node.TryGetName(out string third));
        Assert.Equal("After", third); // and released, which is equally the point
    }

    [Fact]
    public void Snapshot_RefusesToNest()
    {
        SyntheticScene scene = new(densePages: true);
        scene.NewNode("Game");
        SceneEpoch epoch = scene.BeginEpoch();

        using MemorySnapshot first = epoch.Snapshot();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => epoch.Snapshot());
        Assert.Contains("already has snapshot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EndingTheEpoch_EndsTheSnapshotWithIt()
    {
        SyntheticScene scene = new(densePages: true);
        scene.NewNode("Game");
        SceneEpoch epoch = scene.BeginEpoch();

        MemorySnapshot snapshot = epoch.Snapshot();
        epoch.End();

        Assert.Null(epoch.CurrentSnapshot);
        Assert.Throws<ObjectDisposedException>(() => snapshot.TryRead(Base, new byte[8]));
    }

    [Fact]
    public void Invalidate_ObservesTheTargetAgain()
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Before");
        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));

        using MemorySnapshot snapshot = epoch.Snapshot();
        Assert.True(node.TryGetName(out _));

        scene.SetName(root, "After");
        snapshot.Invalidate();

        Assert.True(node.TryGetName(out string name));
        Assert.Equal("After", name);
    }

    [Fact]
    public void AgreeTwice_IsSuppressedInsideACoherentSnapshot_AndCounted()
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Game");
        ulong child = scene.NewNode("Child", root);
        scene.SetChildren(root, child);

        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));

        // Uncached: the check runs, so the child list is traversed twice.
        int before = scene.Source.ReadCount;
        node.GetChildren();
        int uncachedReads = scene.Source.ReadCount - before;

        using MemorySnapshot snapshot = epoch.Snapshot();
        NodeChildren children = node.GetChildren();

        Assert.Equal(ChildWalkStatus.Complete, children.Status);
        Assert.Equal(1, snapshot.Statistics.AgreeTwiceSuppressed);

        // And the suppression is not free-floating bookkeeping: the second traversal really is gone.
        Assert.True(snapshot.Statistics.LogicalReads < uncachedReads);
    }

    [Fact]
    public void RepeatedReads_CountsTheCheckTheLibraryCannotDecideFor()
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Game", control: true, size: (100f, 50f));

        SceneEpoch epoch = scene.BeginEpoch();
        GodotControl control = epoch.ControlUnchecked(new NativePtr(root));

        using MemorySnapshot snapshot = epoch.Snapshot(new MemoryCacheOptions
        {
            Mode = MemoryCacheMode.Hybrid,
            DetectRepeatedReads = true,
        });

        Assert.True(control.TryGetSize(out GodotVector2 first));
        scene.SetControlGeometry(root, (999f, 999f), (0f, 0f));
        Assert.True(control.TryGetSize(out GodotVector2 second));

        // The second reading is the first reading. Nothing can stop a caller writing this; the
        // counter is what makes it visible.
        Assert.Equal(first.X, second.X);
        Assert.Equal(1, snapshot.Statistics.RepeatedReads);
    }

    [Fact]
    public void StaleReads_AreCounted_ButNeverSilentlyRefreshed()
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Game");
        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));

        using MemorySnapshot snapshot = epoch.Snapshot(new MemoryCacheOptions { MaxAge = TimeSpan.Zero });
        Assert.True(node.TryGetName(out _));

        scene.SetName(root, "Changed");
        Assert.True(node.TryGetName(out string name));

        Assert.True(snapshot.IsStale);
        Assert.True(snapshot.Statistics.StaleReads > 0);
        Assert.Equal("Game", name); // reported, not repaired
    }

    [Theory]
    [InlineData(MemoryCacheMode.None)]
    [InlineData(MemoryCacheMode.Page)]
    [InlineData(MemoryCacheMode.Span)]
    [InlineData(MemoryCacheMode.Hybrid)]
    public void EveryCacheMode_WalksTheSameTreeAsNoCache(MemoryCacheMode mode)
    {
        SyntheticScene scene = new(densePages: true);
        ulong root = scene.NewNode("Root");
        ulong a = scene.NewNode("A", root);
        ulong b = scene.NewNode("B", root);
        ulong c = scene.NewNode("C", a);
        scene.SetChildren(root, a, b);
        scene.SetChildren(a, c);

        SceneEpoch epoch = scene.BeginEpoch();
        using MemorySnapshot snapshot = epoch.Snapshot(new MemoryCacheOptions { Mode = mode });

        TreeWalkResult walk = epoch.SceneFrom(new NativePtr(root)).Walk();
        List<string> names = [];
        foreach (GodotNode node in walk.Nodes)
        {
            Assert.True(node.TryGetName(out string name));
            names.Add(name);
        }

        Assert.Equal(ChildWalkStatus.Complete, walk.WorstStatus);
        Assert.Equal(["Root", "A", "B", "C"], names);
    }

    [Fact]
    public void Hybrid_ServesAnAddressFromWhicheverPolicyMaterialisedItFirst()
    {
        // A read through the page policy, then a node registered whose span covers the same address.
        // Two fetch policies over one address must not produce two moments.
        PagedSource source = new();
        source.MapPage(Base);
        source.MapPage(Base + 4096);
        source.WriteUInt64(Base + 0x30, 0x1111);

        int span = ObjectSpanCache.SpanBytesFor(GodotAbiProfiles.Godot451Release.Offsets, realSize: 4);
        using ObjectSpanCache cache = new(source, MemoryCacheMode.Hybrid, span);

        Assert.True(cache.TryReadUInt64(Base + 0x30, out ulong first));
        Assert.Equal(0x1111UL, first);

        source.WriteUInt64(Base + 0x30, 0x2222);
        cache.RegisterObject(Base);

        Assert.True(cache.TryReadUInt64(Base + 0x30, out ulong second));
        Assert.Equal(0x1111UL, second);
    }

    /// <summary>
    /// A page-granular byte source. <see cref="FakeByteSource"/> is sparse per byte, which no real
    /// target is: a 4 KiB fetch there would fail on the padding between the fields a test wrote, and
    /// the page variants would never be exercised at all.
    /// </summary>
    private sealed class PagedSource : IByteSource
    {
        private const int Size = 4096;
        private readonly Dictionary<ulong, byte[]> _pages = [];

        public bool Is64Bit => true;

        public int Reads { get; private set; }

        public void MapPage(ulong address) => _pages.TryAdd(address & ~(ulong)(Size - 1), new byte[Size]);

        public void WriteUInt64(ulong address, ulong value)
        {
            byte[] page = _pages[address & ~(ulong)(Size - 1)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
                page.AsSpan((int)(address & (Size - 1))), value);
        }

        public bool TryRead(ulong address, Span<byte> buffer)
        {
            Reads++;

            int copied = 0;
            while (copied < buffer.Length)
            {
                ulong cursor = address + (ulong)copied;
                if (!_pages.TryGetValue(cursor & ~(ulong)(Size - 1), out byte[]? page))
                {
                    return false;
                }

                int offset = (int)(cursor & (Size - 1));
                int take = Math.Min(Size - offset, buffer.Length - copied);
                page.AsSpan(offset, take).CopyTo(buffer.Slice(copied, take));
                copied += take;
            }

            return true;
        }
    }
}
