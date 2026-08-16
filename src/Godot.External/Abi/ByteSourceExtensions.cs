using System.Buffers.Binary;

namespace Godot.External.Abi;

/// <summary>
/// Typed reads over <see cref="IByteSource"/>. Everything is little-endian x64: the only validated
/// target is x64 Windows (docs/analysis.md §12.1), and §4.1 concluded that hardcoding the pointer
/// width deletes half the complexity rather than carrying an untested 32-bit path.
/// </summary>
internal static class ByteSourceExtensions
{
    /// <summary>
    /// Pointer width, fixed at 8. Also the width of CowData's <c>USize</c>.
    /// </summary>
    /// <remarks>
    /// A 32-bit target is <b>refused</b>, not guessed at: see <see cref="IsSupportedTarget"/>. Every
    /// offset in every shipped profile was recovered from an x64 binary, so a 32-bit layout would
    /// need its own calibrated profile anyway — half-supporting it would only produce plausible
    /// garbage.
    /// </remarks>
    public const int PointerWidth = 8;

    /// <summary>
    /// Whether this library will read from the target at all. False for 32-bit targets, which no
    /// profile describes and no test covers.
    /// </summary>
    public static bool IsSupportedTarget(this IByteSource source) => source.Is64Bit;

    /// <summary>Reads a native pointer. Fails on an unsupported target rather than assuming a width.</summary>
    public static bool TryReadPointer(this IByteSource source, ulong address, out ulong value)
    {
        if (!source.IsSupportedTarget())
        {
            value = 0;
            return false;
        }

        return source.TryReadUInt64(address, out value);
    }

    /// <summary>Reads a 64-bit unsigned integer (CowData's <c>USize</c>).</summary>
    public static bool TryReadUInt64(this IByteSource source, ulong address, out ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (!source.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return true;
    }

    /// <summary>Reads a 32-bit unsigned integer (a UTF-32 code unit).</summary>
    public static bool TryReadUInt32(this IByteSource source, ulong address, out uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!source.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return true;
    }

    /// <summary>Reads a single-precision <c>real_t</c>.</summary>
    public static bool TryReadSingle(this IByteSource source, ulong address, out float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!source.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadSingleLittleEndian(buffer);
        return true;
    }

    /// <summary>
    /// Reads a <c>real_t</c> whose width is decided by the build's precision. Godot's
    /// <c>real_t</c> is <c>float</c> in single-precision builds and <c>double</c> in
    /// double-precision builds, which moves every field after it (§8.9 lists precision as a
    /// compat-matrix axis).
    /// </summary>
    public static bool TryReadReal(this IByteSource source, ulong address, GodotPrecision precision, out double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        int width = RealSize(precision);

        if (!source.TryRead(address, buffer[..width]))
        {
            value = 0;
            return false;
        }

        value = DecodeReal(buffer, 0, precision);
        return true;
    }

    /// <summary>
    /// Reads a <c>CanvasItem::visible</c>-style C++ <c>bool</c> (one byte); scry reads it through
    /// the byte-sized vtable slot <c>+0x00</c> (§4.6).
    /// </summary>
    public static bool TryReadBoolean(this IByteSource source, ulong address, out bool value)
    {
        Span<byte> buffer = stackalloc byte[1];
        if (!source.TryRead(address, buffer))
        {
            value = false;
            return false;
        }

        value = buffer[0] != 0;
        return true;
    }

    /// <summary>Width of one <c>real_t</c> for the given precision.</summary>
    public static int RealSize(GodotPrecision precision) => precision == GodotPrecision.Double ? 8 : 4;

    /// <summary>
    /// Decodes element <paramref name="index"/> of a <c>real_t</c> array out of an
    /// already-fetched block. Used so vectors and <c>offset[4]</c> come from <b>one</b> remote read:
    /// a per-component read can tear between x and y and yield two plausible halves of different
    /// samples (§12.4e).
    /// </summary>
    public static double DecodeReal(ReadOnlySpan<byte> block, int index, GodotPrecision precision)
    {
        int width = RealSize(precision);
        ReadOnlySpan<byte> element = block.Slice(index * width, width);

        return precision == GodotPrecision.Double
            ? BinaryPrimitives.ReadDoubleLittleEndian(element)
            : BinaryPrimitives.ReadSingleLittleEndian(element);
    }
}
