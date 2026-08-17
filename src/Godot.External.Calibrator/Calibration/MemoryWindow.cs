using System.Buffers.Binary;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>
/// One object's bytes, fetched once, with a per-byte record of what was actually readable.
/// </summary>
/// <remarks>
/// <para>
/// Scanning an object one field at a time would be correct and unusably slow: the semantic passes
/// here try every four-byte alignment across ~3 KiB on twenty nodes. One read per node per pass is
/// the difference between a connect-time cost and a minute.
/// </para>
/// <para>
/// The readable map is the part that matters for correctness. A window that crosses the end of a
/// mapped region is ordinary — an object near a page boundary, a lazily committed tail — and every
/// byte quietly written off there is a candidate that vanishes from an intersection without
/// trace. So unreadable stretches are narrowed by halving rather than discarded wholesale, and
/// whatever is still missing is <b>reported</b> through <see cref="Complete"/> and carried into
/// <see cref="OffsetCandidates.CompleteCoverage"/>.
/// </para>
/// </remarks>
public sealed class MemoryWindow
{
    private const int MinChunk = 8;

    private readonly byte[] _bytes;
    private readonly bool[] _readable;

    private MemoryWindow(ulong baseAddress, byte[] bytes, bool[] readable, bool complete)
    {
        BaseAddress = baseAddress;
        _bytes = bytes;
        _readable = readable;
        Complete = complete;
    }

    /// <summary>Address the window starts at.</summary>
    public ulong BaseAddress { get; }

    /// <summary>Window length in bytes.</summary>
    public int Length => _bytes.Length;

    /// <summary>True when every byte of the window was readable.</summary>
    public bool Complete { get; }

    /// <summary>Reads a window, narrowing around whatever is unmapped.</summary>
    public static MemoryWindow Read(IMemoryReader reader, ulong address, int length)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        byte[] bytes = new byte[length];
        bool[] readable = new bool[length];

        bool complete = Fill(reader, address, bytes, readable, 0, length);

        // One retry over whatever is still missing. An incomplete window is not merely a gap in the
        // data — it forces every derivation that scanned it to withhold, so a single transient failure
        // can collapse a whole cell. That happened once in four series: node.parent had the right
        // candidates in hand and correctly refused to publish them on an unreadable window. Retrying
        // costs one pass over the holes and removes the entire class of event where the failure was
        // momentary.
        if (!complete)
        {
            complete = true;
            for (int offset = 0; offset < length; offset++)
            {
                if (readable[offset])
                {
                    continue;
                }

                int run = offset;
                while (run < length && !readable[run])
                {
                    run++;
                }

                complete &= Fill(reader, address, bytes, readable, offset, run - offset);
                offset = run;
            }
        }

        return new MemoryWindow(address, bytes, readable, complete);
    }

    /// <summary>An entirely unreadable window, for a sample that could not be fetched at all.</summary>
    public static MemoryWindow Unreadable(ulong address, int length)
        => new(address, new byte[length], new bool[length], complete: false);

    private static bool Fill(IMemoryReader reader, ulong address, byte[] bytes, bool[] readable, int start, int count)
    {
        if (count <= 0)
        {
            return true;
        }

        if (reader.TryRead(address + (ulong)start, bytes.AsSpan(start, count)))
        {
            Array.Fill(readable, true, start, count);
            return true;
        }

        if (count <= MinChunk)
        {
            return false;
        }

        int half = count / 2;
        bool low = Fill(reader, address, bytes, readable, start, half);
        bool high = Fill(reader, address, bytes, readable, start + half, count - half);
        return low && high;
    }

    /// <summary>Whether <paramref name="length"/> bytes at <paramref name="offset"/> were read.</summary>
    public bool IsReadable(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _bytes.Length)
        {
            return false;
        }

        for (int i = 0; i < length; i++)
        {
            if (!_readable[offset + i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads a byte.</summary>
    public bool TryByte(int offset, out byte value)
    {
        value = 0;
        if (!IsReadable(offset, 1))
        {
            return false;
        }

        value = _bytes[offset];
        return true;
    }

    /// <summary>Reads a native pointer.</summary>
    public bool TryPointer(int offset, out ulong value)
    {
        value = 0;
        if (!IsReadable(offset, 8))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(_bytes.AsSpan(offset, 8));
        return true;
    }

    /// <summary>Reads one <c>real_t</c> of the build's width.</summary>
    public bool TryReal(int offset, GodotPrecisionWidth precision, out double value)
    {
        value = 0;
        int width = precision.Size;
        if (!IsReadable(offset, width))
        {
            return false;
        }

        ReadOnlySpan<byte> element = _bytes.AsSpan(offset, width);
        value = width == 8
            ? BinaryPrimitives.ReadDoubleLittleEndian(element)
            : BinaryPrimitives.ReadSingleLittleEndian(element);
        return true;
    }

    /// <summary>Reads <paramref name="count"/> consecutive <c>real_t</c> values.</summary>
    public bool TryReals(int offset, GodotPrecisionWidth precision, int count, Span<double> destination)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryReal(offset + (i * precision.Size), precision, out destination[i]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// The width of one <c>real_t</c>, which is the single axis that moves every float offset in the
/// engine (§8.9 lists precision as its own compat-matrix axis for exactly that reason).
/// </summary>
public readonly record struct GodotPrecisionWidth(int Size)
{
    /// <summary>4-byte <c>real_t</c>.</summary>
    public static GodotPrecisionWidth Single => new(4);

    /// <summary>8-byte <c>real_t</c>.</summary>
    public static GodotPrecisionWidth Double => new(8);

    /// <summary>Picks the width from the cell's precision axis.</summary>
    public static GodotPrecisionWidth For(bool isDouble) => isDouble ? Double : Single;
}
