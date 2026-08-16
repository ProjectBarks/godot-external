using Godot.External.Abi;

namespace Godot.External.Values;

/// <summary>
/// Decodes a Godot <c>StringName</c> — the interned-string handle used for <c>Node::name</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two indirections, per docs/analysis.md §4.6 and validated 8/8 live (§12.3b — <c>FmodBankLoader</c>,
/// <c>AudioManager</c>, <c>Proxy</c>, …):
/// </para>
/// <code>
/// node + NodeName                 -> StringName::_Data*
///      + StringNameDataToBuffer   -> char32_t buffer (a CowData pointer)
///      -> UTF-32 decode
/// </code>
/// <para>
/// Every offset involved — including <see cref="GodotOffsetTable.CowDataSizeBackOffset"/> — is taken
/// from the offset table, never from a duplicated literal, so the §12.5 calibrator can override any
/// of them and see the read path change.
/// </para>
/// <para>
/// A null <c>_Data</c> is an empty StringName, which Godot permits — not a failure. Unlike scry's
/// <c>getName</c>, which walks code units one remote read at a time, this goes through
/// <see cref="CowData"/> and reads the whole buffer at once.
/// </para>
/// <para>
/// Names are worth caching, but only for a <b>scene epoch</b> (§8.8): Godot can free a node and
/// reuse the allocation, so a name cached against a native pointer silently becomes a lie about a
/// different node.
/// </para>
/// </remarks>
internal static class GodotStringName
{
    /// <summary>
    /// Reads the <c>StringName</c> whose <c>_Data</c> pointer is stored at
    /// <paramref name="fieldAddress"/> — i.e. <c>node + offsets.NodeName</c>.
    /// </summary>
    public static bool TryReadFromField(
        IByteSource source,
        GodotOffsetTable offsets,
        ulong fieldAddress,
        out string value,
        int maxCharacters = GodotString.DefaultMaxCharacters)
    {
        ArgumentNullException.ThrowIfNull(offsets);

        value = string.Empty;

        return source.TryReadPointer(fieldAddress, out ulong dataPointer)
            && TryReadFromData(source, offsets, dataPointer, out value, maxCharacters);
    }

    /// <summary>
    /// Reads the <c>StringName</c> given its <c>_Data</c> pointer directly. Zero yields an empty
    /// string and success.
    /// </summary>
    public static bool TryReadFromData(
        IByteSource source,
        GodotOffsetTable offsets,
        ulong dataPointer,
        out string value,
        int maxCharacters = GodotString.DefaultMaxCharacters)
    {
        ArgumentNullException.ThrowIfNull(offsets);

        value = string.Empty;
        if (dataPointer == 0)
        {
            return true;
        }

        if (offsets.StringNameDataToBuffer < 0)
        {
            return false;
        }

        ulong bufferField = dataPointer + (ulong)offsets.StringNameDataToBuffer;

        return source.TryReadPointer(bufferField, out ulong bufferPointer)
            && GodotString.TryRead(source, bufferPointer, out value, maxCharacters, offsets.CowDataSizeBackOffset);
    }

    /// <summary>Reads <c>Node::name</c> for the node at <paramref name="nodeAddress"/>.</summary>
    public static bool TryReadNodeName(
        IByteSource source,
        GodotAbiProfile profile,
        ulong nodeAddress,
        out string value)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return TryReadFromField(
            source,
            profile.Offsets,
            profile.FieldAddress(nodeAddress, GodotField.NodeName),
            out value);
    }
}
