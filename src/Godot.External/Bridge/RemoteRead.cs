using Godot.External.Abi;

namespace Godot.External.Bridge;

/// <summary>
/// The library's entire public dependency on a memory reader: read
/// <c>buffer.Length</c> bytes from <paramref name="address"/> in the target process, returning
/// <see langword="false"/> on any failure.
/// </summary>
/// <remarks>
/// <para>
/// <c>Abi.IByteSource</c> says the same thing but is deliberately <c>internal</c>, because it is the
/// seam that will be re-pointed at LiveClr's <c>IMemoryReader</c> (docs/analysis.md §8.8) and the
/// authors did not want it frozen as public API. A delegate gives callers a way in without widening
/// that interface: a LiveClr adapter is one method group, not a class, and there is nothing here for
/// a future refactor to break.
/// </para>
/// <para>
/// Failure is a <see langword="false"/> return, never an exception — §8.8's error model requires the
/// read path to be inspectable so an overlay can answer a suspect read with "reuse the last good
/// snapshot".
/// </para>
/// <para>
/// This delegate — and the Bridge/Scene/Objects types built on it — is <c>internal</c> for now,
/// matching <c>Values/</c>. §8.8 keeps <c>Godot.External</c> private until the ABI grid shows the
/// calibrator solving unseen layouts, so widening the surface before the LiveClr adapter exists
/// would freeze an API with no consumer. Promoting these types to <c>public</c> is a one-keyword
/// change when that consumer lands.
/// </para>
/// </remarks>
internal delegate bool RemoteRead(ulong address, Span<byte> buffer);

/// <summary>Adapts a <see cref="RemoteRead"/> delegate to the internal <see cref="IByteSource"/>.</summary>
internal sealed class RemoteReadByteSource(RemoteRead read, bool is64Bit) : IByteSource
{
    private readonly RemoteRead _read = read ?? throw new ArgumentNullException(nameof(read));

    public bool Is64Bit { get; } = is64Bit;

    public bool TryRead(ulong address, Span<byte> buffer) => _read(address, buffer);
}
