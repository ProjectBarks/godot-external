using System.Buffers.Binary;
using System.Text;
using Godot.External.Abi;

namespace Godot.External.Values;

/// <summary>
/// Decodes a Godot <c>String</c> — a <c>CowData&lt;char32_t&gt;</c> — out of remote memory.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decode UTF-32 properly.</b> docs/analysis.md §4.6 flags that scry truncates each
/// <c>char32_t</c> to a byte when building the JS string: fine for ASCII, silently lossy for
/// anything else, and lossy in the worst way — the string still looks like a string. §8.9 makes
/// non-ASCII text a required case in the ABI grid for this reason. Code points above the BMP are
/// encoded as surrogate pairs by <see cref="Rune"/>, which .NET's UTF-16 <see cref="string"/>
/// requires.
/// </para>
/// <para>
/// Godot stores a trailing NUL inside the CowData, so <c>size()</c> is one more than the visible
/// length. Decoding therefore stops at the first NUL rather than trusting the count blindly; that
/// also makes the decoder correct if a caller passes a count convention that excludes the
/// terminator.
/// </para>
/// </remarks>
internal static class GodotString
{
    /// <summary>Replacement for a code point that is not a valid Unicode scalar.</summary>
    public const char ReplacementChar = '�';

    /// <summary>
    /// Default cap on decoded characters. Generous for names and UI text; a value above this is far
    /// more likely to be a misread pointer than a real string.
    /// </summary>
    public const int DefaultMaxCharacters = 1 << 16;

    /// <summary>
    /// Reads and decodes the string whose CowData buffer pointer is
    /// <paramref name="bufferPointer"/>.
    /// </summary>
    /// <param name="source">Remote memory.</param>
    /// <param name="bufferPointer">CowData data pointer. Zero is an empty string, not an error.</param>
    /// <param name="value">The decoded string; empty on failure.</param>
    /// <param name="maxCharacters">Bound on the accepted element count.</param>
    /// <param name="sizeBackOffset">
    /// CowData size location, normally <see cref="GodotOffsetTable.CowDataSizeBackOffset"/> so a
    /// calibrated value takes effect.
    /// </param>
    /// <returns><see langword="false"/> only if a remote read failed or the count was implausible.</returns>
    public static bool TryRead(
        IByteSource source,
        ulong bufferPointer,
        out string value,
        int maxCharacters = DefaultMaxCharacters,
        int sizeBackOffset = ByteSourceExtensions.PointerWidth)
    {
        value = string.Empty;

        if (!CowData.TryReadBlock(source, bufferPointer, sizeof(uint), out byte[] block, maxCharacters, sizeBackOffset))
        {
            return false;
        }

        value = DecodeUtf32(block);
        return true;
    }

    /// <summary>
    /// Reads the CowData pointer stored at <paramref name="fieldAddress"/> and decodes the string it
    /// points at. This is the shape of <c>Label::text</c> / <c>RichTextLabel::text</c>: the field
    /// holds the buffer pointer directly.
    /// </summary>
    public static bool TryReadFromField(
        IByteSource source,
        ulong fieldAddress,
        out string value,
        int maxCharacters = DefaultMaxCharacters,
        int sizeBackOffset = ByteSourceExtensions.PointerWidth)
    {
        value = string.Empty;

        return source.TryReadPointer(fieldAddress, out ulong bufferPointer)
            && TryRead(source, bufferPointer, out value, maxCharacters, sizeBackOffset);
    }

    /// <summary>
    /// Reads a Godot <c>String</c> field of an object using a profile — the entry point for
    /// <see cref="GodotField.LabelText"/> and <see cref="GodotField.RichTextLabelText"/>. Both the
    /// field offset and the CowData size offset come from the profile, so calibrating either one
    /// takes effect here.
    /// </summary>
    public static bool TryReadField(
        IByteSource source,
        GodotAbiProfile profile,
        ulong objectAddress,
        GodotField field,
        out string value,
        int maxCharacters = DefaultMaxCharacters)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return TryReadFromField(
            source,
            profile.FieldAddress(objectAddress, field),
            out value,
            maxCharacters,
            profile.Offsets.CowDataSizeBackOffset);
    }

    /// <summary>
    /// Decodes little-endian UTF-32 bytes to a .NET string, stopping at the first NUL. Trailing
    /// bytes that do not form a whole code unit are ignored; invalid scalars become
    /// <see cref="ReplacementChar"/> rather than throwing, because a decode failure must not take
    /// down a polling loop (§8.8's error model).
    /// </summary>
    public static string DecodeUtf32(ReadOnlySpan<byte> block)
    {
        int units = block.Length / sizeof(uint);
        if (units == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(units);
        for (int i = 0; i < units; i++)
        {
            uint codePoint = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(i * sizeof(uint), sizeof(uint)));
            if (codePoint == 0)
            {
                break; // Godot's stored NUL terminator.
            }

            if (Rune.TryCreate(codePoint, out Rune rune))
            {
                // Appends a surrogate PAIR for anything above the BMP. This is the line that scry
                // gets wrong by truncating to a byte.
                builder.Append(rune);
            }
            else
            {
                builder.Append(ReplacementChar);
            }
        }

        return builder.ToString();
    }
}
