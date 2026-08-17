using Godot.External.Abi;

namespace Godot.External.Bench.Fixtures;

/// <summary>
/// Passes reads through to a live target while capturing enough surrounding memory that <b>every</b>
/// cache variant can later be replayed against the recording.
/// </summary>
/// <remarks>
/// <para>
/// Recording only the bytes the uncached run asked for would produce a fixture on which the page
/// variants fail every read — their whole behaviour is to fetch more than was asked for. So the
/// recorder captures the <see cref="CaptureAlignment"/>-aligned block around each read, which by
/// construction contains the aligned block any variant with a page size up to
/// <see cref="CaptureAlignment"/> would fetch, and (given a node span far smaller than that) any
/// object span the read could belong to, give or take the block after it — which is captured too.
/// </para>
/// <para>
/// A page the target refuses is left unmapped rather than zero-filled, so the replay reproduces the
/// failure and the negative-caching paths are exercised in CI rather than only in front of a game.
/// </para>
/// </remarks>
internal sealed class RecordingByteSource(IByteSource inner, MemoryImage image) : IByteSource
{
    /// <summary>
    /// Capture granularity, and therefore the largest page size a recorded fixture can faithfully
    /// replay. 16 KiB covers the swept variants with room to spare.
    /// </summary>
    public const int CaptureAlignment = 16384;

    private readonly IByteSource _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly MemoryImage _image = image ?? throw new ArgumentNullException(nameof(image));
    private readonly HashSet<ulong> _seenBlocks = [];

    /// <inheritdoc/>
    public bool Is64Bit => _inner.Is64Bit;

    /// <summary>Reads that reached the target.</summary>
    public long Reads { get; private set; }

    /// <inheritdoc/>
    public bool TryRead(ulong address, Span<byte> buffer)
    {
        Reads++;

        if (buffer.Length > 0 && address != 0)
        {
            ulong first = address & ~(ulong)(CaptureAlignment - 1);
            ulong last = (address + (ulong)(buffer.Length - 1)) & ~(ulong)(CaptureAlignment - 1);

            // One block past the end as well: an object span starting near a block boundary runs
            // into its neighbour, and the span variants must not be handed a truncated node.
            for (ulong block = first; block <= last; block += CaptureAlignment)
            {
                Capture(block);
            }

            if (last <= ulong.MaxValue - CaptureAlignment)
            {
                Capture(last + CaptureAlignment);
            }
        }

        return _inner.TryRead(address, buffer);
    }

    private void Capture(ulong block)
    {
        if (!_seenBlocks.Add(block))
        {
            return;
        }

        for (int offset = 0; offset < CaptureAlignment; offset += MemoryImage.PageSize)
        {
            _image.CapturePage(_inner, block + (ulong)offset);
        }
    }
}
