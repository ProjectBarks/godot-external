using System.Buffers.Binary;
using System.Text;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>One offset that holds a valid Godot <c>String</c>, and what it held on each node.</summary>
public sealed record TextField(int Offset, IReadOnlyDictionary<ulong, string> Values)
{
    /// <summary>Whether the nodes carrying this offset disagree about its contents.</summary>
    public bool Varies => Values.Values.Distinct().Count() > 1;
}

/// <summary>
/// Candidate text offsets, split by the class layout each one fits.
/// </summary>
/// <param name="Label">Offsets bracketed as <c>Label::text</c>.</param>
/// <param name="RichTextLabel">Offsets bracketed as <c>RichTextLabel::text</c>.</param>
/// <param name="Unbracketed">
/// Every valid string field, whether or not it fits a known class. Used only as a fallback, so a
/// bracket that turns out to be too tight on some build degrades to the previous behaviour instead
/// of publishing nothing.
/// </param>
/// <param name="Rejections">
/// For each offset that held a valid String but failed a class bracket, the clause that rejected
/// it. Without this, "no offset matched Godot's Label layout" is the whole story a result can tell
/// about the cell that gates publication, and finding out which of four clauses did it costs a
/// matrix run.
/// </param>
public sealed record TextFieldSet(
    IReadOnlyList<TextField> Label,
    IReadOnlyList<TextField> RichTextLabel,
    IReadOnlyList<TextField> Unbracketed,
    IReadOnlyDictionary<string, string> Rejections);

/// <summary>
/// Finds <c>Label::text</c> and <c>RichTextLabel::text</c> by validating Godot's own structures,
/// rather than by inference from what happens to decode.
/// </summary>
/// <remarks>
/// <para>
/// The harness supplies node names but no texts, so there is no known value to scan for. What there
/// is instead is layout: <c>CowData</c> has a two-word header, and each of the two classes puts its
/// <c>String</c> in a field neighbourhood that can be recognised. Both are read out of Godot's
/// headers, identical in 4.3 and 4.5.
/// </para>
/// <para>
/// Decoding alone is far too weak, and that weakness was measurable: on a fixed target the same node
/// decoded to <c>"Ā"</c>, then <c>"…翶"</c>, then something longer, on three successive runs. Those
/// are not corrupted reads of the right field — they are different wrong <em>addresses</em>, each
/// one an arbitrary qword whose neighbours happened to look like a string. Validating the header
/// removes them.
/// </para>
/// </remarks>
public static class TextCalibrator
{
    /// <summary>How far into a node to look for string fields; past <c>RichTextLabel::text</c>.</summary>
    public const int DefaultScanBytes = 0x1400;

    /// <summary>Bound on a believable element count and reference count.</summary>
    public const ulong MaxElements = 1 << 20;

    /// <summary>Longest string treated as real UI text rather than a misread pointer.</summary>
    public const int MaxLength = 4096;

    /// <summary>
    /// Distance from a CowData data pointer back to its size field — <b>derived per target</b>, not
    /// a constant.
    /// </summary>
    /// <remarks>
    /// It was a constant 8 here, and that was true for 4.2–4.5 and false for 4.6, where
    /// <c>cowdata.h</c> inserts a <c>capacity</c> field ahead of the size and re-aligns the payload.
    /// See <see cref="GodotText.TryDeriveSizeBackOffset"/>, which measures it from name buffers the
    /// harness already identified; this property only follows what that measured.
    /// </remarks>
    public static int SizeOffset => GodotText.CowDataSizeBackOffset;

    /// <summary>Distance from a CowData data pointer back to its reference count.</summary>
    /// <remarks>
    /// The reference count is the <em>first</em> member of the header
    /// (<c>cowdata.h: REF_COUNT_OFFSET = 0</c>), so this distance is the whole header size, which
    /// <c>cowdata.h</c> computes as the size field's offset plus <c>sizeof(USize)</c> rounded up to
    /// <c>Memory::MAX_ALIGN</c> (16 on the toolchain the official Windows templates are built with).
    /// That reproduces both measurements from the one derived number: 16 when the size sits at
    /// <c>-8</c> (4.2–4.5, header <c>[refcount][size]</c>) and 32 when it sits at <c>-0x10</c>
    /// (4.6, header <c>[refcount][capacity][size][pad]</c>).
    /// </remarks>
    public static int RefCountOffset => (SizeOffset + 8 + 15) & ~15;

    /// <summary>Discovers and classifies every valid string field on the given nodes.</summary>
    public static TextFieldSet Discover(
        IMemoryReader reader,
        IReadOnlyList<ulong> nodes,
        int scanBytes = DefaultScanBytes)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(nodes);

        Dictionary<int, Dictionary<ulong, string>> all = [];
        Dictionary<int, Dictionary<ulong, string>> label = [];
        Dictionary<int, Dictionary<ulong, string>> rich = [];
        Dictionary<string, string> rejections = [];

        foreach (ulong node in nodes)
        {
            for (int offset = 0; offset + 8 <= scanBytes; offset += 8)
            {
                if (!TryReadTextField(reader, node + (ulong)offset, out string value, out ulong buffer))
                {
                    continue;
                }

                Add(all, offset, node, value);

                if (FitsLabel(reader, node, offset, buffer, out string labelReason))
                {
                    Add(label, offset, node, value);
                }
                else
                {
                    Note(rejections, offset, node, "label", labelReason);
                }

                if (FitsRichTextLabel(reader, node, offset, out string richReason))
                {
                    Add(rich, offset, node, value);
                }
                else
                {
                    Note(rejections, offset, node, "richTextLabel", richReason);
                }
            }
        }

        return new TextFieldSet(Fields(label), Fields(rich), Fields(all), rejections);
    }

    /// <summary>
    /// Reads a Godot <c>String</c> field, rejecting anything whose <c>CowData</c> header is not one.
    /// </summary>
    /// <remarks>
    /// The header is two independent qwords ahead of the data — <c>[refcount][size]</c> — and the
    /// buffer always ends in a stored NUL, because Godot's <c>length()</c> is <c>size() - 1</c>. An
    /// arbitrary pointer has to satisfy all of that at once, plus 16-byte alignment, plus every code
    /// unit being a real Unicode scalar. scry checks none of it: one <c>ptr == 0</c> early-out is its
    /// entire safety story.
    /// </remarks>
    public static bool TryReadTextField(IMemoryReader reader, ulong fieldAddress, out string value, out ulong buffer)
    {
        ArgumentNullException.ThrowIfNull(reader);

        value = string.Empty;
        buffer = 0;

        if (!reader.TryReadPointer(fieldAddress, out ulong pointer) || pointer == 0 || pointer % 16 != 0)
        {
            return false;
        }

        if (!TryReadUInt64(reader, pointer - (ulong)SizeOffset, out ulong size)
            || size < 1
            || size > MaxElements
            || size > MaxLength)
        {
            return false;
        }

        if (!TryReadUInt64(reader, pointer - (ulong)RefCountOffset, out ulong refCount) || refCount < 1 || refCount > MaxElements)
        {
            return false;
        }

        byte[] block = new byte[(int)size * sizeof(uint)];
        if (!ReadWithRetry(reader, pointer, block))
        {
            return false;
        }

        // Godot stores the terminator INSIDE the CowData, so the last element must be it.
        if (BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(((int)size - 1) * sizeof(uint))) != 0)
        {
            return false;
        }

        StringBuilder builder = new((int)size);
        for (int i = 0; i < (int)size - 1; i++)
        {
            uint unit = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(i * sizeof(uint)));
            if (unit > 0x10FFFF || (unit >= 0xD800 && unit <= 0xDFFF) || !Rune.IsValid((int)unit))
            {
                return false;
            }

            builder.Append(new Rune(unit));
        }

        value = builder.ToString();
        buffer = pointer;
        return IsPlausibleText(value);
    }

    /// <summary>
    /// Whether the field at <paramref name="offset"/> sits where <c>Label::text</c> sits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Godot's <c>Label</c> declares <c>String text</c> and, unconditionally in both 4.3 and 4.5,
    /// <c>String xl_text</c> immediately after it, sharing one allocation while nothing is
    /// translated. The shared pointer is the class signature, and it also settles which of the two is
    /// <c>text</c>: only the lower one has the packed alignment enums behind it, because the upper
    /// one has a pointer there.
    /// </para>
    /// <para>
    /// <b>Nothing here asserts that any byte is zero.</b> A <c>+0x14 must be reserved</c> clause was
    /// tried and was false: that dword is a live member past <c>xl_text</c>, measured holding
    /// <c>0x39</c>, <c>0x42</c>, <c>0x51</c>, <c>0x5a</c>, <c>0x79</c>, <c>0</c> and larger values,
    /// varying between two Labels in one process. Because the class rule is subset-not-equality, one
    /// Label happening to hold zero there was enough to publish — so the reference cell was not
    /// failing to find the bracket, it was losing a coin flip. This is the third derivation in this
    /// calibrator to have been broken by assuming C++ zeroes what nobody wrote.
    /// </para>
    /// <para>
    /// The bracket around it is the neighbouring layout: two alignment enums packed in the qword
    /// before, <c>autowrap_mode</c> sixteen bytes after, and a zero word after that.
    /// </para>
    /// </remarks>
    public static bool FitsLabel(IMemoryReader reader, ulong node, int offset, ulong buffer, out string reason)
    {
        ArgumentNullException.ThrowIfNull(reader);

        reason = string.Empty;

        if (offset < 8)
        {
            reason = "too close to the start of the object to have a bracket";
            return false;
        }

        if (!TryReadUInt64(reader, node + (ulong)offset - 8, out ulong alignments))
        {
            reason = "the qword behind it could not be read";
            return false;
        }

        if ((alignments & 0xFFFFFFFF) > 3 || (alignments >> 32) > 3)
        {
            reason = $"the qword behind it (0x{alignments:x}) is not two alignment enums";
            return false;
        }

        if (!TryReadUInt32(reader, node + (ulong)offset + 16, out uint autowrap) || autowrap > 3)
        {
            reason = $"+0x10 is {autowrap}, not an autowrap_mode enum";
            return false;
        }

        // A Label declares String text and String xl_text back to back, and with nothing translated
        // atr() returns the same string by value, so CowData::_ref shares the allocation and the two
        // fields hold the IDENTICAL pointer. That was measured directly in target memory — 0x7f8 and
        // 0x800 both reading 0x207057987f0, on every Label instance, under BOTH bindings. It is the
        // strongest clause in this bracket and it is not binding-dependent.
        if (!reader.TryReadPointer(node + (ulong)offset + 8, out ulong translated) || translated != buffer)
        {
            reason = $"+0x8 holds 0x{translated:x}, not the same allocation as +0x0 (0x{buffer:x}), so this is "
                   + "not Label's text/xl_text pair";
            return false;
        }

        return true;
    }

    /// <summary>
    /// There is no measured bracket for <c>RichTextLabel::text</c>, so nothing here pretends there is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two clauses used to stand here — <c>use_bbcode</c> in the qword behind, a run of bools in the
    /// qword after — and the first was measured rejecting the <b>right answer on every build tested</b>,
    /// including the reference cell's <c>0xa78</c>, which is the value the shipped profile itself
    /// names. The qwords behind it read <c>0xd000000000000</c>, <c>0x8b0bd70000000000</c>,
    /// <c>0x4925040000000000</c>: whatever precedes this field, it is not a boolean.
    /// </para>
    /// <para>
    /// The second clause was never disproved — but it was never measured either, and it only escaped
    /// blame because the first one short-circuited before it ran. Three derivations in this
    /// calibrator have now been broken by asserting a neighbour's value instead of measuring it
    /// (<c>visible</c>'s zero padding, <c>Label::text+0x14</c>, and this), so the honest position is
    /// to assert nothing about neighbours at all.
    /// </para>
    /// <para>
    /// What is left is not weak: the offset must be above every derived <c>Node</c>/<c>CanvasItem</c>/
    /// <c>Control</c> member, hold a structurally valid <c>CowData&lt;char32_t&gt;</c> (aligned
    /// pointer, plausible refcount and size, stored NUL terminator, real Unicode scalars), and decode
    /// on <em>none</em> of the nodes the engine does not call <c>RichTextLabel</c>. That last one is
    /// the discriminator with teeth, and it grows teeth as instances are added — which is why a
    /// second RichTextLabel in the scene is worth more here than any clause could be.
    /// </para>
    /// </remarks>
    public static bool FitsRichTextLabel(IMemoryReader reader, ulong node, int offset, out string reason)
    {
        ArgumentNullException.ThrowIfNull(reader);

        reason = string.Empty;
        return offset >= 8;
    }

    /// <summary>Whether a decoded string looks like UI text rather than a decode accident.</summary>
    public static bool IsPlausibleText(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (Rune rune in value.EnumerateRunes())
        {
            if (rune.Value < 0x20 && rune.Value is not ('\t' or '\n' or '\r'))
            {
                return false;
            }
        }

        return true;
    }

    private static void Note(Dictionary<string, string> rejections, int offset, ulong node, string kind, string reason)
    {
        if (reason.Length == 0)
        {
            return;
        }

        // One reason per offset per class: the first node to reject it is as informative as the
        // twentieth, and this has to stay small enough to put in a result.
        rejections.TryAdd($"{kind} 0x{offset:x}", $"{reason} (on node 0x{node:x})");
    }

    private static void Add(Dictionary<int, Dictionary<ulong, string>> map, int offset, ulong node, string value)
    {
        if (!map.TryGetValue(offset, out Dictionary<ulong, string>? values))
        {
            values = [];
            map[offset] = values;
        }

        values[node] = value;
    }

    private static IReadOnlyList<TextField> Fields(Dictionary<int, Dictionary<ulong, string>> map)
        => [.. map.OrderBy(kv => kv.Key).Select(kv => new TextField(kv.Key, kv.Value))];

    /// <summary>
    /// Reads, retrying a failure before believing it.
    /// </summary>
    /// <remarks>
    /// A read that fails is not evidence that a field is absent, and treating it as such is why the
    /// SAME offset decoded one Label on one run and the other Label on the next, against an
    /// unchanging binary. The offset was right and stable both times; only the read was flaky, and
    /// the flake was silently reported as "this node has no text".
    /// </remarks>
    public static bool ReadWithRetry(IMemoryReader reader, ulong address, Span<byte> buffer, int attempts = 3)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (reader.TryRead(address, buffer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadUInt64(IMemoryReader reader, ulong address, out ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (!ReadWithRetry(reader, address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return true;
    }

    private static bool TryReadUInt32(IMemoryReader reader, ulong address, out uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!reader.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return true;
    }
}
