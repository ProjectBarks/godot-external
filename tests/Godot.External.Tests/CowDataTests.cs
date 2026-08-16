using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// docs/analysis.md §4.6: CowData stores <c>[refcount][size]</c> ahead of the data and hands out a
/// pointer to the data, so the length is read <em>behind</em> the pointer.
/// </summary>
public class CowDataTests
{
    private const ulong Buffer = 0x2000;

    [Fact]
    public void ElementCount_IsReadEightBytesBeforeTheBufferPointer()
    {
        FakeByteSource source = new FakeByteSource()
            .WriteUInt64(Buffer - 8, 5)
            .WriteUInt64(Buffer, 0xDEADBEEF); // data, deliberately not a count

        Assert.True(CowData.TryReadElementCount(source, Buffer, out int count));
        Assert.Equal(5, count);
    }

    [Fact]
    public void NullPointer_IsAnEmptyBuffer_NotAFailure()
    {
        FakeByteSource source = new();

        Assert.True(CowData.TryReadElementCount(source, 0, out int count));
        Assert.Equal(0, count);

        Assert.True(CowData.TryReadBlock(source, 0, sizeof(uint), out byte[] block));
        Assert.Empty(block);
    }

    [Fact]
    public void ImplausibleCount_IsRejectedRatherThanAllocated()
    {
        FakeByteSource source = new FakeByteSource().WriteUInt64(Buffer - 8, ulong.MaxValue);

        Assert.False(CowData.TryReadElementCount(source, Buffer, out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void UnreadableSizeField_Fails()
    {
        // Data present, header page missing — a plausible partial mapping.
        FakeByteSource source = new FakeByteSource().WriteUInt32(Buffer, 0x41);

        Assert.False(CowData.TryReadElementCount(source, Buffer, out _));
    }

    [Fact]
    public void Block_IsFetchedInASingleBulkRead()
    {
        // §4.6 calls out scry's per-character getName walk as the wrong approach. One read for the
        // count, one for the whole block — never one per element.
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, "Controller Detected");

        int before = source.ReadCount;
        Assert.True(CowData.TryReadBlock(source, Buffer, sizeof(uint), out byte[] block));

        Assert.Equal(2, source.ReadCount - before);
        Assert.Equal("Controller Detected".Length * 4 + 4, block.Length); // includes the NUL element
    }

    [Fact]
    public void CountAboveCallerBound_IsRejected()
    {
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, new string('a', 64));

        Assert.False(CowData.TryReadBlock(source, Buffer, sizeof(uint), out _, maxElements: 8));
    }
}
