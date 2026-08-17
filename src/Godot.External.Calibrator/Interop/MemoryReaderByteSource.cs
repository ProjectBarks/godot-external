using Godot.External.Abi;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Interop;

/// <summary>
/// The one-file adapter <see cref="IByteSource"/>'s own documentation anticipates: LiveClr's
/// <see cref="IMemoryReader"/> presented as the two-member seam <c>Godot.External</c> reads through.
/// </summary>
/// <remarks>
/// docs/analysis.md §8.8 puts the real reader (ReadProcessMemory plus a page cache) in LiveClr and
/// keeps <c>Godot.External</c> free of any dependency on it. That separation only survives if the
/// join lives here, in the calibrator, rather than in the library.
/// </remarks>
internal sealed class MemoryReaderByteSource(IMemoryReader reader) : IByteSource
{
    private readonly IMemoryReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public bool Is64Bit => _reader.Is64Bit;

    public bool TryRead(ulong address, Span<byte> buffer) => _reader.TryRead(address, buffer);
}
