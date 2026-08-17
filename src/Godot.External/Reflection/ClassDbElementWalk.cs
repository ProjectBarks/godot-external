using Godot.External.Abi;

namespace Godot.External.Reflection;

/// <summary>
/// Enumerates every entry of a Godot <c>HashMap</c> from <b>one</b> of its elements, without ever
/// locating the map object.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNVALIDATED — this has never been run against a live Godot process.</b> The layout it walks is
/// read out of <c>core/templates/hash_map.h</c> (MIT) and the tests below drive it against synthetic
/// memory, which proves the walker, not the premise. Nothing here may feed a
/// <c>GodotAbiProfile</c> until a real run confirms it. The gap is deliberate and named: see
/// <see cref="Abi.AbiConfidence.Unvalidated"/>, and docs/analysis.md §8.9 on publishing one validated
/// cell of a matrix as if it were general.
/// </para>
/// <para>
/// <b>The mechanism.</b> Godot's <c>HashMap</c> keeps its entries in an intrusive doubly-linked list:
/// <c>HashMapElement</c> declares <c>next</c>, then <c>prev</c>, then its <c>KeyValue data</c>, with
/// no vtable. So a single element address is a complete handle on the container — walk <c>prev</c> to
/// the head, then <c>next</c> to the tail, and every entry has been visited. The map object, its
/// bucket array, its hash function and its capacity are all irrelevant, which removes the layout
/// this route would otherwise be most likely to get wrong.
/// </para>
/// <para>
/// <b>What is still missing, and why the chain is not wired up.</b> Obtaining that first element is
/// the unsolved half. The intended path — a known <c>Object</c>'s <c>_class_name_ptr</c> to its
/// static <c>StringName</c>, to the interned <c>_Data*</c>, then to whichever element's key holds
/// that pointer — needs a memory <em>scan</em> to find the element, and no scanner lives in this
/// assembly (the calibrator's does, in another project, and pulling it down here would invert the
/// dependency). Until that seed exists this walker has no production caller, which is the correct
/// state for unvalidated code.
/// </para>
/// </remarks>
internal static class ClassDbElementWalk
{
    /// <summary>
    /// Bound on entries. <c>ClassDB</c> registers on the order of a thousand classes; anything past
    /// this is a corrupt chain, not a bigger engine.
    /// </summary>
    public const int DefaultMaxElements = 8192;

    /// <summary>
    /// Walks the whole chain containing <paramref name="seedElement"/>, in list order.
    /// </summary>
    /// <param name="source">Target memory.</param>
    /// <param name="seedElement">Any <c>HashMapElement*</c> in the chain.</param>
    /// <param name="layout">Element offsets for the target's engine version.</param>
    /// <param name="elements">Element addresses, head to tail. Possibly partial — check the return.</param>
    /// <param name="reason">Why the walk stopped. Empty on a clean traversal.</param>
    /// <param name="maxElements">Bound, so a corrupt link cannot spin.</param>
    /// <returns><see langword="false"/> when the chain was truncated, looped or unreadable.</returns>
    public static bool TryEnumerate(
        IByteSource source,
        ulong seedElement,
        ClassDbLayout layout,
        out IReadOnlyList<ulong> elements,
        out string reason,
        int maxElements = DefaultMaxElements)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxElements);

        elements = [];

        if (!source.IsSupportedTarget())
        {
            reason = "target is not 64-bit";
            return false;
        }

        if (seedElement == 0)
        {
            reason = "no seed element";
            return false;
        }

        if (!TryWalkToHead(source, seedElement, layout, maxElements, out ulong head, out reason))
        {
            return false;
        }

        List<ulong> found = [];
        HashSet<ulong> seen = [];
        ulong cursor = head;

        while (cursor != 0)
        {
            if (!IsPlausibleElement(cursor))
            {
                reason = $"misaligned element 0x{cursor:x}; the chain is torn or the layout is wrong";
                elements = found;
                return false;
            }

            if (!seen.Add(cursor))
            {
                reason = $"cycle at 0x{cursor:x}";
                elements = found;
                return false;
            }

            if (found.Count >= maxElements)
            {
                reason = $"more than {maxElements} elements; refusing to keep following the chain";
                elements = found;
                return false;
            }

            found.Add(cursor);

            if (!source.TryReadPointer(cursor + (ulong)layout.ElementNext, out ulong next))
            {
                reason = $"read failed following next from 0x{cursor:x}";
                elements = found;
                return false;
            }

            cursor = next;
        }

        elements = found;
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads the key pointer of one element — for <c>ClassDB::classes</c> the interned
    /// <c>StringName::_Data*</c> naming the class.
    /// </summary>
    public static bool TryReadKeyPointer(
        IByteSource source,
        ulong element,
        ClassDbLayout layout,
        out ulong key)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(layout);

        key = 0;
        return source.IsSupportedTarget()
            && source.TryReadPointer(element + (ulong)layout.ElementData, out key);
    }

    private static bool TryWalkToHead(
        IByteSource source,
        ulong seedElement,
        ClassDbLayout layout,
        int maxElements,
        out ulong head,
        out string reason)
    {
        head = seedElement;
        HashSet<ulong> seen = [];

        for (int step = 0; step <= maxElements; step++)
        {
            if (!IsPlausibleElement(head))
            {
                reason = $"misaligned element 0x{head:x} walking back to the head";
                return false;
            }

            if (!seen.Add(head))
            {
                reason = $"cycle at 0x{head:x} walking back to the head";
                return false;
            }

            if (!source.TryReadPointer(head + (ulong)layout.ElementPrevious, out ulong previous))
            {
                reason = $"read failed following prev from 0x{head:x}";
                return false;
            }

            if (previous == 0)
            {
                reason = string.Empty;
                return true;
            }

            head = previous;
        }

        reason = $"walked back more than {maxElements} links without reaching a head";
        return false;
    }

    // Godot's allocator never returns a misaligned element, so this catches a torn read or a wrong
    // layout before it becomes a plausible-looking address.
    private static bool IsPlausibleElement(ulong address)
        => address != 0 && (address & (ByteSourceExtensions.PointerWidth - 1)) == 0;
}
