namespace Godot.External.Abi;

/// <summary>
/// The entire memory-access dependency of this library: fill a caller-supplied buffer from a
/// remote address, out of process, read-only.
/// </summary>
/// <remarks>
/// <para>
/// Kept to two members on purpose. docs/analysis.md §8.8 puts the real reader in LiveClr
/// (<c>IMemoryReader</c>, backed by <c>ReadProcessMemory</c> plus a page cache); this interface is a
/// placeholder that will be replaced by a one-file adapter once this repo references it. Nothing in
/// <c>Abi/</c> or <c>Values/</c> may grow a dependency on anything richer than this.
/// </para>
/// <para>
/// Failure is reported as <see langword="false"/>, never as an exception. §8.8 ("Error model")
/// requires validation to be inspectable rather than throwing: an overlay's correct response to a
/// suspect read is "reuse the last good snapshot", which is impossible if the read path throws.
/// </para>
/// </remarks>
internal interface IByteSource
{
    /// <summary>
    /// Reads exactly <c>buffer.Length</c> bytes starting at <paramref name="address"/>.
    /// Returns <see langword="false"/> on any failure (unmapped page, short read, torn region);
    /// the buffer contents are then undefined.
    /// </summary>
    bool TryRead(ulong address, Span<byte> buffer);

    /// <summary>
    /// Pointer width of the target process. Drives pointer reads and the width of the
    /// <c>CowData</c> size field, which is <c>USize</c> (8 bytes on x64, 4 on x86).
    /// </summary>
    bool Is64Bit { get; }
}
