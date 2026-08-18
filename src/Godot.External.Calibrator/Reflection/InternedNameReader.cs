using System.Text;
using Godot.External.Abi;
using Godot.External.Values;

namespace Godot.External.Calibrator.Reflection;

/// <summary>
/// Reads the text out of an interned <c>StringName::_Data</c>, following the same
/// <c>cname ? String(cname) : name</c> rule the engine's own <c>_Data::get_name()</c> uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both members must be tried, and the version does not decide which one is populated — the
/// interning route does.</b> <c>StringName</c>'s <c>StaticCString</c> constructor sets <c>cname</c>
/// and leaves <c>name</c> empty; the <c>String</c> and <c>const char *</c> constructors do the
/// reverse. Measured on a live 4.3 target this cuts both ways inside a <em>single</em> walk: the
/// class-name keys of <c>ClassDB::classes</c> carry their text in <c>name</c>, and the method-name
/// keys of <c>ClassInfo::method_map</c> carry theirs in <c>cname</c>. A <c>name</c>-only 4.3 reader
/// therefore reads every class name correctly and every method name as the empty string, which
/// resolves no bind at all while looking like a working walk (docs/analysis.md §16.5).
/// </para>
/// <para>
/// <see cref="NameOffset"/> is <b>measured per target</b>, not assumed: the seed discovers it by
/// finding the slot that holds the pointer to a known class name's UTF-32 buffer. Assuming it is
/// how a candidate one pool slot low reads the neighbouring interned name and returns a real,
/// plausible, wrong string for every key (§16.1).
/// </para>
/// </remarks>
internal sealed class InternedNameReader
{
    /// <summary>Longest name accepted. Class and method names are short; anything longer is noise.</summary>
    public const int MaxCharacters = 256;

    private readonly IByteSource _source;

    /// <summary>Creates a reader over a measured <c>_Data</c> layout.</summary>
    /// <param name="source">Target memory.</param>
    /// <param name="nameOffset">Offset of <c>_Data::name</c> (a Godot <c>String</c>), as measured.</param>
    /// <param name="cnameOffset">
    /// Offset of <c>_Data::cname</c>, or a negative number on a version that dropped it.
    /// </param>
    /// <param name="cowDataSizeBackOffset">Distance back from a CowData buffer to its element count.</param>
    public InternedNameReader(IByteSource source, int nameOffset, int cnameOffset, int cowDataSizeBackOffset)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        NameOffset = nameOffset;
        CompileTimeNameOffset = cnameOffset;
        CowDataSizeBackOffset = cowDataSizeBackOffset;
    }

    /// <summary>Measured offset of <c>_Data::name</c>.</summary>
    public int NameOffset { get; }

    /// <summary>Measured offset of <c>_Data::cname</c>, negative when the version has none.</summary>
    public int CompileTimeNameOffset { get; }

    /// <summary>Distance back from a CowData buffer pointer to its element count.</summary>
    public int CowDataSizeBackOffset { get; }

    /// <summary>Reads the name an interned <c>_Data*</c> stands for.</summary>
    /// <returns><see langword="false"/> when neither member yielded a non-empty string.</returns>
    public bool TryRead(ulong data, out string value)
    {
        value = string.Empty;

        if (data == 0 || (data & 7) != 0 || NameOffset < 0)
        {
            return false;
        }

        // cname first, exactly as get_name() does. The order is not cosmetic: on 4.3 the method-name
        // keys have cname populated and name null, so a name-first reader that stops at "name was
        // readable" would take the empty string and never look further.
        if (CompileTimeNameOffset >= 0
            && _source.TryReadPointer(data + (ulong)CompileTimeNameOffset, out ulong cname)
            && cname != 0
            && TryReadAscii(cname, out value))
        {
            return true;
        }

        return _source.TryReadPointer(data + (ulong)NameOffset, out ulong buffer)
            && buffer != 0
            && GodotString.TryRead(_source, buffer, out value, MaxCharacters, CowDataSizeBackOffset)
            && value.Length > 0;
    }

    private bool TryReadAscii(ulong address, out string value)
    {
        value = string.Empty;
        Span<byte> buffer = stackalloc byte[64];

        if (!_source.TryRead(address, buffer))
        {
            return false;
        }

        int length = buffer.IndexOf((byte)0);
        if (length <= 0)
        {
            return false;
        }

        foreach (byte b in buffer[..length])
        {
            // A compile-time StringName is an ASCII identifier. Anything else means this qword was a
            // heap pointer that happened to be non-null, not a `const char *`.
            if (b is < 0x20 or > 0x7e)
            {
                return false;
            }
        }

        value = Encoding.ASCII.GetString(buffer[..length]);
        return true;
    }
}
