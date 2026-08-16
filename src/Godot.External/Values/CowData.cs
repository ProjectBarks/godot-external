using Godot.External.Abi;

namespace Godot.External.Values;

/// <summary>
/// Reads Godot's <c>CowData&lt;T&gt;</c> — the copy-on-write buffer behind <c>String</c>,
/// <c>Vector</c> and friends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The element count lives <em>before</em> the buffer pointer.</b> CowData allocates
/// <c>[refcount][size][elements…]</c> and the pointer it hands out addresses the <em>elements</em>,
/// so the count is read at <c>ptr - sizeof(USize)</c> (docs/analysis.md §4.6; validated 5/5 on live
/// <c>Label</c> text in §12.3b). A null pointer is a legitimately empty buffer, not a failure.
/// </para>
/// <para>
/// Reads are <b>bulk</b>: one remote read for the whole element block. scry's <c>getText</c> does
/// this; its <c>getName</c> instead walks code units one remote read at a time, which §4.6 calls out
/// as noticeably worse. Per-element reads are also a consistency hazard — a long walk widens the
/// window in which the target mutates underneath us (§12.4e).
/// </para>
/// </remarks>
internal static class CowData
{
    /// <summary>
    /// Refuse to allocate for absurd counts. A torn or misinterpreted read produces a huge
    /// <c>size</c> far more often than a legitimately huge string does, and a bounded refusal is
    /// cheaper to diagnose than an <see cref="OutOfMemoryException"/>.
    /// </summary>
    public const int MaxElements = 1 << 20;

    /// <summary>
    /// Reads the element count stored ahead of <paramref name="bufferPointer"/>.
    /// </summary>
    /// <param name="source">Remote memory.</param>
    /// <param name="bufferPointer">The CowData data pointer. Zero means empty.</param>
    /// <param name="count">Element count; 0 for a null buffer.</param>
    /// <param name="sizeBackOffset">
    /// Distance back to the size field, normally taken from
    /// <see cref="GodotOffsetTable.CowDataSizeBackOffset"/>. Matches §4.6's <c>read(buf - 8)</c>.
    /// </param>
    /// <returns><see langword="false"/> on an unsupported target, or a failed or implausible read.</returns>
    public static bool TryReadElementCount(
        IByteSource source,
        ulong bufferPointer,
        out int count,
        int sizeBackOffset = ByteSourceExtensions.PointerWidth)
    {
        count = 0;
        if (!source.IsSupportedTarget())
        {
            return false;
        }

        if (bufferPointer == 0)
        {
            return true;
        }

        if (sizeBackOffset <= 0 || bufferPointer < (ulong)sizeBackOffset)
        {
            return false;
        }

        if (!source.TryReadUInt64(bufferPointer - (ulong)sizeBackOffset, out ulong raw))
        {
            return false;
        }

        if (raw > MaxElements)
        {
            return false;
        }

        count = (int)raw;
        return true;
    }

    /// <summary>
    /// Reads a CowData block in a single remote read: count first, then
    /// <c>count * elementSize</c> bytes.
    /// </summary>
    /// <param name="source">Remote memory.</param>
    /// <param name="bufferPointer">The CowData data pointer. Zero yields an empty block.</param>
    /// <param name="elementSize">Bytes per element (4 for <c>char32_t</c>).</param>
    /// <param name="block">The raw element bytes, exactly <c>count * elementSize</c> long.</param>
    /// <param name="maxElements">Upper bound on accepted counts; see <see cref="MaxElements"/>.</param>
    /// <param name="sizeBackOffset">Distance back to the size field; see the other overload.</param>
    /// <returns><see langword="false"/> if either read fails or the count is implausible.</returns>
    public static bool TryReadBlock(
        IByteSource source,
        ulong bufferPointer,
        int elementSize,
        out byte[] block,
        int maxElements = MaxElements,
        int sizeBackOffset = ByteSourceExtensions.PointerWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementSize);

        block = [];
        if (!TryReadElementCount(source, bufferPointer, out int count, sizeBackOffset))
        {
            return false;
        }

        if (count > maxElements)
        {
            return false;
        }

        if (count == 0)
        {
            return true;
        }

        long byteLength = (long)count * elementSize;
        if (byteLength > int.MaxValue)
        {
            return false;
        }

        byte[] buffer = new byte[(int)byteLength];

        // One read for the whole block — see the remarks on this class.
        if (!source.TryRead(bufferPointer, buffer))
        {
            return false;
        }

        block = buffer;
        return true;
    }
}
