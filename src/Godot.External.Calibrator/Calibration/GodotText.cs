using System.Buffers.Binary;
using System.Text;
using Godot.External.Calibrator.Interop;
using Godot.External.Values;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>
/// Godot string decoding, over a candidate offset rather than a profile.
/// </summary>
/// <remarks>
/// The decoding itself is <c>Godot.External</c>'s — <c>CowData</c> length read at <c>buf - 8</c>,
/// one bulk read of <c>len * 4</c> bytes, proper UTF-32 with surrogate pairs. §4.6 found scry
/// truncating <c>char32_t</c> to a byte on both its string paths, silently, so the decoder is
/// exactly the thing not to reimplement here.
/// </remarks>
public static class GodotText
{
    /// <summary>Distance back from a CowData buffer pointer to its element count, on x64.</summary>
    public const int CowDataSizeBackOffset = 8;

    /// <summary>Decodes the <c>String</c> whose CowData buffer pointer is stored at a field address.</summary>
    public static bool TryReadStringField(IMemoryReader reader, ulong fieldAddress, out string value)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return GodotString.TryReadFromField(
            new MemoryReaderByteSource(reader),
            fieldAddress,
            out value,
            GodotString.DefaultMaxCharacters,
            CowDataSizeBackOffset);
    }

    /// <summary>
    /// Decodes a <c>StringName</c> given the slot holding its <c>_Data</c> pointer and the distance
    /// from <c>_Data</c> to the character buffer.
    /// </summary>
    public static bool TryReadStringName(IMemoryReader reader, ulong fieldAddress, int dataToBuffer, out string value)
    {
        ArgumentNullException.ThrowIfNull(reader);

        value = string.Empty;
        MemoryReaderByteSource source = new(reader);
        if (!reader.TryReadPointer(fieldAddress, out ulong data) || data == 0)
        {
            return false;
        }

        return reader.TryReadPointer(data + (ulong)dataToBuffer, out ulong buffer)
            && GodotString.TryRead(source, buffer, out value, GodotString.DefaultMaxCharacters, CowDataSizeBackOffset);
    }

    /// <summary>
    /// Derives the <c>StringName::_Data</c> → buffer distance from a slot whose decoded name is
    /// already known. A known value, so a lone match is not trusted blindly — the caller intersects
    /// across nodes.
    /// </summary>
    public static IReadOnlyList<int> DataToBufferCandidates(IMemoryReader reader, ulong fieldAddress, string expectedName)
    {
        ArgumentNullException.ThrowIfNull(reader);

        List<int> candidates = [];
        for (int k = 0; k <= 0x20; k += 8)
        {
            if (TryReadStringName(reader, fieldAddress, k, out string name) && name == expectedName)
            {
                candidates.Add(k);
            }
        }

        return candidates;
    }

    /// <summary>
    /// The little-endian UTF-32 bytes of <paramref name="value"/>, NUL-terminated — the needle a
    /// whole-process scan looks for when nothing about the target's layout is known yet.
    /// </summary>
    public static byte[] Utf32Needle(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        List<byte> bytes = [];
        foreach (Rune rune in value.EnumerateRunes())
        {
            byte[] unit = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(unit, (uint)rune.Value);
            bytes.AddRange(unit);
        }

        bytes.AddRange([0, 0, 0, 0]);
        return [.. bytes];
    }
}
