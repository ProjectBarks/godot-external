using Godot.External.Abi;

namespace Godot.External.Reflection;

/// <summary>The validated header of one <c>AHashMap</c>.</summary>
/// <param name="Elements">
/// <c>_elements</c> — the base of the dense <c>KeyValue</c> array. Entry <c>i</c> is at
/// <c>Elements + i * keyValueStride</c>, for <c>i</c> in <c>[0, Size)</c>.
/// </param>
/// <param name="Metadata">
/// <c>_metadata</c> — the open-addressing index, <c>Capacity</c> entries of 8 bytes. Never walked
/// here; it is read only to prove the header is one.
/// </param>
/// <param name="Capacity">Real capacity, i.e. <c>_capacity_mask + 1</c>. Always a power of two.</param>
/// <param name="Size">Live entry count.</param>
public readonly record struct AHashMapHeader(ulong Elements, ulong Metadata, uint Capacity, uint Size);

/// <summary>
/// Enumerates a Godot 4.6 <c>AHashMap</c>, which is a <b>dense array plus an index</b> and not a
/// linked structure at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> At 4.6 <c>ClassInfo</c> converts three of its maps from <c>HashMap</c> to
/// <c>AHashMap</c> — <c>constant_map</c>, <c>signal_map</c> and <c>property_setget</c>
/// (<c>class_db.h:132,139,157</c>). <see cref="ClassDbElementWalk"/> cannot read them: it follows
/// <c>HashMapElement::next</c>/<c>prev</c>, and an <c>AHashMap</c> entry has neither, so pointing it
/// at one would traverse whatever the first two qwords of a <c>KeyValue</c> happen to be.
/// </para>
/// <para>
/// <b>The layout</b>, from <c>core/templates/a_hash_map.h:96-103</c>:
/// </para>
/// <code>
/// MapKeyValue *_elements;      // +0x00   dense, index 0.._size-1
/// Metadata    *_metadata;      // +0x08   { uint32 hash; uint32 element_idx; }, static_assert(sizeof == 8)
/// uint32_t     _capacity_mask; // +0x10   capacity - 1, so capacity is a power of two
/// uint32_t     _size;          // +0x14
/// </code>
/// <para>
/// <b>Measured</b> on a live 4.6.3-stable release template, release and debug, by requiring all four
/// members to agree and then reading the keys back: three such headers exist in every
/// <c>ClassInfo</c> (at <c>+0x78</c>, <c>+0xb8</c> and <c>+0x100</c> on release, <c>+0x78</c>,
/// <c>+0xb8</c> and <c>+0x1b8</c> on debug), <c>sizeof(AHashMap)</c> is <b>24</b>, and the same
/// detector finds <b>zero</b> such headers in any of 4.5's 908 <c>ClassInfo</c>s — which is the
/// control that makes the 4.6 count evidence rather than an artefact of a loose predicate.
/// </para>
/// <para>
/// <b>What this walker gives up.</b> §13.4's useful property of the <c>HashMap</c> chain — find one
/// element and enumerate the container without ever locating the map object — is gone, because there
/// is nothing to walk back from. An <c>AHashMap</c> must be addressed as a map. That costs nothing
/// here: every <c>AHashMap</c> this route reads lives inside a <c>ClassInfo</c> the caller already
/// holds, so its address is free. It would matter if <c>ClassDB::classes</c> itself became one, and
/// it has not — that is still a <c>HashMap</c> at 4.6, 4.7 and master, so the seed chain keeps the
/// property.
/// </para>
/// <para>
/// <b>The stride is the caller's, and it is not knowable here.</b> Entry <c>i</c> is at
/// <c>_elements + i * sizeof(KeyValue&lt;TKey, TValue&gt;)</c>, and <c>TValue</c> differs per map
/// (<c>int64_t</c> for <c>constant_map</c>, <c>MethodInfo</c> for <c>signal_map</c>,
/// <c>PropertySetGet</c> for <c>property_setget</c>). There is no version constant that covers them,
/// so the stride is a parameter and a wrong one is the caller's error to make — which is why
/// <see cref="TryEnumerate"/> validates the whole span it is about to hand back rather than trusting
/// the arithmetic.
/// </para>
/// </remarks>
internal static class AHashMapWalk
{
    /// <summary><c>_elements</c>: the dense array base.</summary>
    public const int ElementsPointer = 0x00;

    /// <summary><c>_metadata</c>: the open-addressing index.</summary>
    public const int MetadataPointer = 0x08;

    /// <summary><c>_capacity_mask</c>: capacity minus one.</summary>
    public const int CapacityMask = 0x10;

    /// <summary><c>_size</c>: live entries.</summary>
    public const int SizeField = 0x14;

    /// <summary><c>sizeof(AHashMap)</c> — two pointers and two dwords. Measured 24 on 4.6.3.</summary>
    public const int Size = 24;

    /// <summary>
    /// <c>AHashMap::INITIAL_CAPACITY</c> (<c>a_hash_map.h:85</c>). A live map never has fewer
    /// buckets, so a "capacity" below this is a misread header rather than a small map.
    /// </summary>
    public const int InitialCapacity = 16;

    /// <summary>
    /// Bound on capacity. <c>ClassInfo</c>'s maps hold tens to hundreds of entries; a capacity past
    /// this is a wrong address that happened to satisfy the power-of-two test.
    /// </summary>
    public const uint MaxCapacity = 1u << 20;

    /// <summary>
    /// Reads and validates an <c>AHashMap</c> header, without touching the entries.
    /// </summary>
    /// <remarks>
    /// Every clause here is a property of the structure, not of the data: both pointers non-null and
    /// pointer-aligned, <c>capacity</c> a power of two at or above the declared initial capacity, and
    /// <c>size</c> within it. An arbitrary 24-byte window has to satisfy all of them at once, and the
    /// 4.5 control run says that essentially never happens — 0 hits across 908 <c>ClassInfo</c>s.
    /// </remarks>
    /// <param name="source">Target memory.</param>
    /// <param name="map">Address of the <c>AHashMap</c> itself.</param>
    /// <param name="header">The validated header. Meaningless unless this returns true.</param>
    /// <param name="reason">Which clause rejected it. Empty on success.</param>
    public static bool TryReadHeader(IByteSource source, ulong map, out AHashMapHeader header, out string reason)
    {
        ArgumentNullException.ThrowIfNull(source);

        header = default;

        if (!source.IsSupportedTarget())
        {
            reason = "target is not 64-bit";
            return false;
        }

        if (map == 0)
        {
            reason = "no map address";
            return false;
        }

        if (!source.TryReadPointer(map + ElementsPointer, out ulong elements)
            || !source.TryReadPointer(map + MetadataPointer, out ulong metadata)
            || !source.TryReadUInt32(map + CapacityMask, out uint mask)
            || !source.TryReadUInt32(map + SizeField, out uint size))
        {
            reason = $"read failed on the AHashMap header at 0x{map:x}";
            return false;
        }

        // An AHashMap that has never held an entry has both pointers null. That is a legitimate
        // empty map, not a bad address, and the two cases are worth separating in the reason.
        if (elements == 0 || metadata == 0)
        {
            reason = size == 0
                ? "the map has never been populated (_elements and _metadata are null)"
                : $"_size is {size} but the storage pointers are null; this is not an AHashMap";
            return false;
        }

        if (!IsPointerAligned(elements) || !IsPointerAligned(metadata))
        {
            reason = $"_elements 0x{elements:x} / _metadata 0x{metadata:x} are not pointer-aligned";
            return false;
        }

        if (mask == uint.MaxValue)
        {
            reason = "_capacity_mask is 0xffffffff, so capacity would overflow";
            return false;
        }

        uint capacity = mask + 1;
        if (capacity < InitialCapacity || capacity > MaxCapacity || (capacity & (capacity - 1)) != 0)
        {
            reason = $"capacity {capacity} is not a power of two in [{InitialCapacity}, {MaxCapacity}]";
            return false;
        }

        if (size > capacity)
        {
            reason = $"_size {size} exceeds capacity {capacity}";
            return false;
        }

        // The index is capacity * sizeof(Metadata) bytes and sizeof(Metadata) is static_asserted at
        // 8. Requiring its last entry to be readable is what separates a real map from a pair of
        // plausible pointers, and it costs one read.
        if (!source.TryReadUInt64(metadata + ((ulong)capacity - 1) * 8, out _))
        {
            reason = $"the _metadata index does not span {capacity} entries from 0x{metadata:x}";
            return false;
        }

        header = new AHashMapHeader(elements, metadata, capacity, size);
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Enumerates the addresses of the live <c>KeyValue</c> entries, in dense-array order.
    /// </summary>
    /// <param name="source">Target memory.</param>
    /// <param name="map">Address of the <c>AHashMap</c>.</param>
    /// <param name="keyValueStride"><c>sizeof(KeyValue&lt;TKey, TValue&gt;)</c> for this map.</param>
    /// <param name="entries">Entry addresses. Empty unless this returns true.</param>
    /// <param name="reason">Why the enumeration was refused. Empty on success.</param>
    /// <returns>
    /// <see langword="false"/> whenever the header does not validate or any entry in the span is
    /// unreadable. <b>A partial list is never returned</b>: unlike a linked chain, where "the first
    /// n links then a torn read" is a real diagnosis, a dense array either spans readable memory or
    /// the base pointer is wrong, and half of a wrong array is not half an answer.
    /// </returns>
    public static bool TryEnumerate(
        IByteSource source,
        ulong map,
        int keyValueStride,
        out IReadOnlyList<ulong> entries,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keyValueStride);

        entries = [];

        if (!TryReadHeader(source, map, out AHashMapHeader header, out reason))
        {
            return false;
        }

        // The dense array is allocated for the whole capacity, but only _size of it is live, and
        // reading past _size is reading destructed or never-constructed KeyValues.
        List<ulong> found = new((int)header.Size);
        for (uint i = 0; i < header.Size; i++)
        {
            ulong entry = header.Elements + ((ulong)i * (ulong)keyValueStride);
            if (!source.TryReadPointer(entry, out _))
            {
                reason = $"entry {i} of {header.Size} at 0x{entry:x} is unreadable; "
                    + $"stride 0x{keyValueStride:x} does not describe this map";
                entries = [];
                return false;
            }

            found.Add(entry);
        }

        entries = found;
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads the key pointer of one entry.
    /// </summary>
    /// <remarks>
    /// <c>KeyValue</c> declares <c>key</c> first, and every <c>AHashMap</c> in <c>ClassInfo</c> is
    /// keyed by <c>StringName</c>, which is one <c>_Data*</c>. So the key is the entry's first
    /// qword, and unlike <see cref="ClassDbElementWalk.TryReadKeyPointer"/> there is no version-
    /// dependent element header to skip past first.
    /// </remarks>
    public static bool TryReadKeyPointer(IByteSource source, ulong entry, out ulong key)
    {
        ArgumentNullException.ThrowIfNull(source);

        key = 0;
        return source.IsSupportedTarget() && source.TryReadPointer(entry, out key);
    }

    private static bool IsPointerAligned(ulong address)
        => (address & (ByteSourceExtensions.PointerWidth - 1)) == 0;
}
