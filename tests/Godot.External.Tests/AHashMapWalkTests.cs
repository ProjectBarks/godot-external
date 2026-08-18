using Godot.External.Reflection;

namespace Godot.External.Tests;

/// <summary>
/// The 4.6 <c>AHashMap</c> reader: a dense array plus an open-addressed index, with no links to
/// follow.
/// </summary>
/// <remarks>
/// <para>
/// <b>These fixtures are synthetic; the numbers they encode are not.</b> The header shape, the
/// 24-byte size and the three per-<c>ClassInfo</c> instances were read off running 4.6.3-stable
/// export templates (release and debug), with 4.5 run through the same detector as a control — it
/// finds none there. What is synthetic is only the memory these tests lay out, so that the refusal
/// paths can be driven without a game.
/// </para>
/// </remarks>
public sealed class AHashMapWalkTests
{
    private const ulong Map = 0x400000;
    private const ulong Elements = 0x500000;
    private const ulong Metadata = 0x600000;

    /// <summary><c>KeyValue&lt;StringName, int64_t&gt;</c> — <c>constant_map</c>'s entry, measured at 0x10.</summary>
    private const int ConstantStride = 0x10;

    private static FakeByteSource BuildMap(uint size, uint capacity, int stride = ConstantStride)
    {
        FakeByteSource memory = new();

        memory.WritePointer(Map + AHashMapWalk.ElementsPointer, Elements);
        memory.WritePointer(Map + AHashMapWalk.MetadataPointer, Metadata);
        memory.WriteUInt32(Map + AHashMapWalk.CapacityMask, capacity - 1);
        memory.WriteUInt32(Map + AHashMapWalk.SizeField, size);

        // The index spans the whole capacity, and the walker reads its last entry to prove it.
        for (uint i = 0; i < capacity; i++)
        {
            memory.WriteUInt64(Metadata + (i * 8), 0x1234_0000u | i);
        }

        // Dense storage: key first (an interned StringName::_Data*), then the value.
        for (uint i = 0; i < size; i++)
        {
            memory.WritePointer(Elements + (i * (ulong)stride), 0x900000 + i);
            memory.WriteUInt64(Elements + (i * (ulong)stride) + 8, 1000 + i);
        }

        return memory;
    }

    [Fact]
    public void TheDenseArrayIsEnumeratedInIndexOrder()
    {
        FakeByteSource memory = BuildMap(size: 5, capacity: 16);

        Assert.True(AHashMapWalk.TryEnumerate(
            memory, Map, ConstantStride, out IReadOnlyList<ulong> entries, out string reason));
        Assert.Empty(reason);
        Assert.Equal(5, entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            Assert.Equal(Elements + (ulong)(i * ConstantStride), entries[i]);
            Assert.True(AHashMapWalk.TryReadKeyPointer(memory, entries[i], out ulong key));
            Assert.Equal(0x900000UL + (ulong)i, key);
        }
    }

    [Fact]
    public void OnlyTheLiveEntriesAreVisitedEvenThoughStorageIsAllocatedForTheCapacity()
    {
        // The dense array is capacity-sized; only _size of it holds constructed KeyValues. Reading to
        // the capacity would hand back destructed or never-constructed entries whose key pointers
        // are arbitrary — and, being pointers, arbitrary in the way that looks like a real answer.
        FakeByteSource memory = BuildMap(size: 3, capacity: 64);

        Assert.True(AHashMapWalk.TryEnumerate(memory, Map, ConstantStride, out IReadOnlyList<ulong> entries, out _));
        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void AnEmptyButValidMapIsReportedAsEmptyRatherThanAsAFailure()
    {
        FakeByteSource memory = BuildMap(size: 0, capacity: 16);

        Assert.True(AHashMapWalk.TryEnumerate(memory, Map, ConstantStride, out IReadOnlyList<ulong> entries, out string reason));
        Assert.Empty(entries);
        Assert.Empty(reason);
    }

    [Fact]
    public void ANeverPopulatedMapSaysSoRatherThanLookingLikeABadAddress()
    {
        FakeByteSource memory = BuildMap(size: 0, capacity: 16);
        memory.WritePointer(Map + AHashMapWalk.ElementsPointer, 0);
        memory.WritePointer(Map + AHashMapWalk.MetadataPointer, 0);

        Assert.False(AHashMapWalk.TryReadHeader(memory, Map, out _, out string reason));
        Assert.Contains("never been populated", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonPowerOfTwoCapacityIsRefused()
    {
        // _capacity_mask is capacity-1 by construction, so capacity is always 2^n. A window that
        // fails this is not a small map, it is not a map.
        FakeByteSource memory = BuildMap(size: 2, capacity: 16);
        memory.WriteUInt32(Map + AHashMapWalk.CapacityMask, 100);

        Assert.False(AHashMapWalk.TryReadHeader(memory, Map, out _, out string reason));
        Assert.Contains("power of two", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASizeLargerThanTheCapacityIsRefused()
    {
        FakeByteSource memory = BuildMap(size: 2, capacity: 16);
        memory.WriteUInt32(Map + AHashMapWalk.SizeField, 40);

        Assert.False(AHashMapWalk.TryReadHeader(memory, Map, out _, out string reason));
        Assert.Contains("exceeds capacity", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIndexThatDoesNotSpanItsCapacityIsRefused()
    {
        // This is the clause that separates a real AHashMap from two plausible pointers sitting next
        // to two plausible dwords: the metadata block must actually be capacity*8 bytes of readable
        // memory.
        FakeByteSource memory = BuildMap(size: 2, capacity: 64);
        memory.Unmap(Metadata + (63 * 8), 8);

        Assert.False(AHashMapWalk.TryReadHeader(memory, Map, out _, out string reason));
        Assert.Contains("_metadata index does not span", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AMisalignedStoragePointerIsRefused()
    {
        FakeByteSource memory = BuildMap(size: 2, capacity: 16);
        memory.WritePointer(Map + AHashMapWalk.ElementsPointer, Elements + 3);

        Assert.False(AHashMapWalk.TryReadHeader(memory, Map, out _, out string reason));
        Assert.Contains("pointer-aligned", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AWrongStrideIsRefusedRatherThanReturningAShortList()
    {
        // The stride is the caller's to supply and there is no version constant for it — TValue
        // differs per map. A stride that walks off the allocation must fail loudly: a truncated dense
        // scan is indistinguishable from a smaller map, which is §12.4e's failure in a new container.
        FakeByteSource memory = BuildMap(size: 4, capacity: 16);

        Assert.False(AHashMapWalk.TryEnumerate(
            memory, Map, keyValueStride: 0x40, out IReadOnlyList<ulong> entries, out string reason));
        Assert.Empty(entries);
        Assert.Contains("does not describe this map", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeaderIsTwentyFourBytesAndTheMembersAreWhereAHashMapDeclaresThem()
    {
        Assert.Equal(0x00, AHashMapWalk.ElementsPointer);
        Assert.Equal(0x08, AHashMapWalk.MetadataPointer);
        Assert.Equal(0x10, AHashMapWalk.CapacityMask);
        Assert.Equal(0x14, AHashMapWalk.SizeField);
        Assert.Equal(24, AHashMapWalk.Size);

        // Measured on 4.6.3: the three AHashMaps inside a release ClassInfo sit at +0x78, +0xb8 and
        // +0x100, and the gap between the two HashMaps that straddle constant_map is 0x40 — one
        // 40-byte HashMap plus one 24-byte AHashMap. That arithmetic is the size measurement.
        Assert.Equal(0x40, ClassDbLayout.Godot46.HashMapSize + AHashMapWalk.Size);
    }

    [Fact]
    public void AnUnreadableMapRefuses()
        => Assert.False(AHashMapWalk.TryReadHeader(new FakeByteSource(), Map, out _, out string reason)
            || !reason.Contains("read failed", StringComparison.Ordinal));

    [Fact]
    public void ANullMapRefuses()
        => Assert.False(AHashMapWalk.TryEnumerate(new FakeByteSource(), 0, ConstantStride, out _, out _));
}
