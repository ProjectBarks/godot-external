using System.Buffers.Binary;
using System.Text;
using Godot.External.Abi;

namespace Godot.External.Tests;

/// <summary>
/// A synthetic <see cref="IByteSource"/> over a sparse byte map, so decoders can be tested without a
/// live game — the fixture idea from docs/analysis.md §8.8 ("Two additions"), minimal form.
/// </summary>
/// <remarks>
/// Sparse on purpose: unmapped addresses fail the read, which is how an out-of-process reader
/// actually behaves and is what the failure-path tests need. <see cref="ReadCount"/> exists to prove
/// the CowData path performs <b>one</b> bulk read rather than per-character reads (§4.6).
/// </remarks>
internal sealed class FakeByteSource : IByteSource
{
    private readonly Dictionary<ulong, byte> _bytes = [];

    public bool Is64Bit { get; init; } = true;

    /// <summary>Number of <see cref="TryRead"/> calls so far.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Invoked before each read with (address, length) — used to simulate live mutation.</summary>
    public Action<ulong, int>? OnRead { get; set; }

    public bool TryRead(ulong address, Span<byte> buffer)
    {
        ReadCount++;
        OnRead?.Invoke(address, buffer.Length);

        for (int i = 0; i < buffer.Length; i++)
        {
            if (!_bytes.TryGetValue(address + (ulong)i, out byte value))
            {
                return false;
            }

            buffer[i] = value;
        }

        return true;
    }

    public FakeByteSource WriteBytes(ulong address, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            _bytes[address + (ulong)i] = data[i];
        }

        return this;
    }

    public FakeByteSource WritePointer(ulong address, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return WriteBytes(address, Is64Bit ? buffer : buffer[..4]);
    }

    public FakeByteSource WriteUInt64(ulong address, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return WriteBytes(address, buffer);
    }

    public FakeByteSource WriteUInt32(ulong address, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        return WriteBytes(address, buffer);
    }

    public FakeByteSource WriteSingle(ulong address, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        return WriteBytes(address, buffer);
    }

    public FakeByteSource WriteDouble(ulong address, double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        return WriteBytes(address, buffer);
    }

    /// <summary>
    /// Lays out a Godot <c>String</c>: element count at
    /// <c>bufferAddress - sizeBackOffset</c>, then the UTF-32 code units.
    /// <paramref name="includeTerminator"/> reproduces Godot's convention where the stored size
    /// counts the trailing NUL, and <paramref name="sizeBackOffset"/> defaults to the x64
    /// <c>USize</c> width — the only width the library accepts.
    /// </summary>
    public FakeByteSource WriteGodotString(
        ulong bufferAddress,
        string value,
        bool includeTerminator = true,
        int sizeBackOffset = 8)
    {
        List<uint> codePoints = [];
        foreach (Rune rune in value.EnumerateRunes())
        {
            codePoints.Add((uint)rune.Value);
        }

        if (includeTerminator)
        {
            codePoints.Add(0);
        }

        WriteUInt64(bufferAddress - (ulong)sizeBackOffset, (ulong)codePoints.Count);
        for (int i = 0; i < codePoints.Count; i++)
        {
            WriteUInt32(bufferAddress + (ulong)(i * 4), codePoints[i]);
        }

        return this;
    }

    /// <summary>Removes a mapping, so reads through it fail (an unmapped page).</summary>
    public FakeByteSource Unmap(ulong address, int length)
    {
        for (int i = 0; i < length; i++)
        {
            _bytes.Remove(address + (ulong)i);
        }

        return this;
    }
}
