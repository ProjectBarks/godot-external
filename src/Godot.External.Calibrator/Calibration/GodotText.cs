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
/// <para>
/// The decoding itself is <c>Godot.External</c>'s — <c>CowData</c> length read at
/// <c>buf - CowDataSizeBackOffset</c>, one bulk read of <c>len * 4</c> bytes, proper UTF-32 with
/// surrogate pairs. §4.6 found scry truncating <c>char32_t</c> to a byte on both its string paths,
/// silently, so the decoder is exactly the thing not to reimplement here.
/// </para>
/// <para>
/// <b>The distance back to the length is not a constant across engine versions, and pretending it
/// was cost a whole version.</b> Through 4.5 the <c>CowData</c> header is
/// <c>[refcount][size][data]</c> with the data pointer 16 bytes in, so the element count sits at
/// <c>buf - 8</c>. Godot 4.6 inserts a <c>capacity</c> field
/// (<c>core/templates/cowdata.h</c>: <c>REF_COUNT_OFFSET / CAPACITY_OFFSET / SIZE_OFFSET /
/// DATA_OFFSET</c>) and re-aligns the payload to <c>Memory::MAX_ALIGN</c>, which on the toolchain
/// the official Windows templates are built with is 16 — so the header is 32 bytes and the count
/// moves to <c>buf - 16</c>. A hardcoded 8 therefore reads the trailing padding on 4.6, every
/// string fails to decode, and the calibrator reports "found no StringName slot" for a name that is
/// plainly present in memory.
/// </para>
/// <para>
/// So it is <b>derived</b>, from buffers whose text — and therefore whose element count — the
/// harness already stated. See <see cref="TryDeriveSizeBackOffset"/>. The default is only what
/// applies until a derivation runs.
/// </para>
/// </remarks>
public static class GodotText
{
    /// <summary>
    /// The distance that applied on every version this project measured before 4.6. It is the
    /// starting value, not an assumption anything is allowed to keep: <see cref="TryDeriveSizeBackOffset"/>
    /// replaces it from the target.
    /// </summary>
    public const int DefaultCowDataSizeBackOffset = 8;

    /// <summary>Distances a derivation will consider, in bytes back from the buffer pointer.</summary>
    public static readonly int[] CandidateSizeBackOffsets = [8, 16, 24, 32];

    private static int _cowDataSizeBackOffset = DefaultCowDataSizeBackOffset;

    /// <summary>Distance back from a CowData buffer pointer to its element count, on x64.</summary>
    public static int CowDataSizeBackOffset => _cowDataSizeBackOffset;

    /// <summary>True once <see cref="TryDeriveSizeBackOffset"/> has replaced the default.</summary>
    public static bool CowDataSizeBackOffsetWasDerived { get; private set; }

    /// <summary>
    /// Derives the CowData buffer → element-count distance from character buffers whose text is
    /// already known, and adopts it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The input is raw byte-scan hits: addresses at which the NUL-terminated UTF-32 of a known name
    /// occurs. No layout is assumed to produce them, which is what makes them usable to establish
    /// one. A real <c>CowData&lt;char32_t&gt;</c> buffer holding <c>"AlphaPanel"</c> has the value
    /// <c>11</c> somewhere in the header in front of it; a copy of those bytes sitting in a resource
    /// blob does not.
    /// </para>
    /// <para>
    /// <b>Two names, not one.</b> A single length is matched by any header field that happens to hold
    /// that number, and small integers are everywhere. Requiring the same distance to explain names
    /// of <em>different lengths</em> is what makes the answer a measurement: <c>12</c> in front of
    /// <c>"RootHarness"</c> and <c>11</c> in front of <c>"AlphaPanel"</c> at the same displacement is
    /// the size field and nothing else is. A distance supported by only one name, or two distances
    /// with equal support, is refused rather than resolved by preference.
    /// </para>
    /// </remarks>
    /// <param name="reader">Target memory.</param>
    /// <param name="buffersByName">Raw scan hits per known name.</param>
    /// <param name="derived">The adopted distance, when this returns true.</param>
    /// <param name="reason">What was measured, or why nothing was adopted.</param>
    public static bool TryDeriveSizeBackOffset(
        IMemoryReader reader,
        IReadOnlyDictionary<string, IReadOnlyList<ulong>> buffersByName,
        out int derived,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(buffersByName);

        derived = DefaultCowDataSizeBackOffset;

        // For each candidate distance: which of the supplied names had at least one buffer whose
        // header held exactly that name's element count there.
        Dictionary<int, HashSet<string>> namesExplained = [];
        Dictionary<int, int> buffersExplained = [];

        foreach ((string name, IReadOnlyList<ulong> buffers) in buffersByName)
        {
            ulong expected = (ulong)name.Length + 1;   // CowData<char32_t> counts the NUL
            foreach (ulong buffer in buffers)
            {
                foreach (int back in CandidateSizeBackOffsets)
                {
                    if (buffer < (ulong)back || !reader.TryReadPointer(buffer - (ulong)back, out ulong value))
                    {
                        continue;
                    }

                    if (value != expected)
                    {
                        continue;
                    }

                    if (!namesExplained.TryGetValue(back, out HashSet<string>? names))
                    {
                        names = [];
                        namesExplained[back] = names;
                    }

                    names.Add(name);
                    buffersExplained[back] = buffersExplained.GetValueOrDefault(back) + 1;
                }
            }
        }

        // Distinct LENGTHS, not distinct names: two names of the same length prove nothing a single
        // one did not, and the whole point of the second name is that its count is a different
        // number.
        static int DistinctLengths(HashSet<string> names) => names.Select(n => n.Length).Distinct().Count();

        List<int> best = [.. namesExplained
            .Where(kv => DistinctLengths(kv.Value) >= 2)
            .OrderByDescending(kv => DistinctLengths(kv.Value))
            .ThenByDescending(kv => buffersExplained[kv.Key])
            .Select(kv => kv.Key)];

        if (best.Count == 0)
        {
            reason = "CowData size distance: no displacement in "
                + $"{{{string.Join(", ", CandidateSizeBackOffsets.Select(b => $"-0x{b:x}"))}}} held the element count "
                + $"for two names of different lengths across {buffersByName.Sum(kv => kv.Value.Count)} raw buffer hit(s); "
                + $"keeping the default -0x{DefaultCowDataSizeBackOffset:x}.";
            return false;
        }

        if (best.Count > 1
            && DistinctLengths(namesExplained[best[0]]) == DistinctLengths(namesExplained[best[1]])
            && buffersExplained[best[0]] == buffersExplained[best[1]])
        {
            reason = $"CowData size distance: -0x{best[0]:x} and -0x{best[1]:x} are equally well supported, "
                + "which is ambiguous rather than resolvable; keeping the default "
                + $"-0x{DefaultCowDataSizeBackOffset:x}.";
            return false;
        }

        derived = best[0];
        _cowDataSizeBackOffset = derived;
        CowDataSizeBackOffsetWasDerived = true;
        reason = $"CowData element count is at buffer-0x{derived:x}, derived from "
            + $"{DistinctLengths(namesExplained[derived])} name(s) of different lengths over "
            + $"{buffersExplained[derived]} buffer(s)"
            + (derived == DefaultCowDataSizeBackOffset
                ? " (the 4.2-4.5 header: refcount, size, data)."
                : " — NOT the pre-4.6 -0x8; Godot 4.6's CowData header carries a capacity field and "
                  + "aligns the payload to Memory::MAX_ALIGN.");
        return true;
    }

    /// <summary>Resets the derived distance. For tests, and for a second target in one process.</summary>
    public static void ResetSizeBackOffset()
    {
        _cowDataSizeBackOffset = DefaultCowDataSizeBackOffset;
        CowDataSizeBackOffsetWasDerived = false;
    }

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
