using Godot.External.Abi;

namespace Godot.External.Bench.Measurement;

/// <summary>
/// Counts what actually reached the target. Installed <b>under</b> the cache, so its numbers are the
/// syscalls and bytes the operating system saw — the quantities the cache exists to reduce, and the
/// ones a cache's own self-reported statistics cannot be trusted to get right.
/// </summary>
/// <remarks>
/// The library's <c>CacheStatistics</c> reports the same figures. That redundancy is deliberate: the
/// benchmark asserts the two agree, so a counter bug in the cache shows up as a discrepancy rather
/// than as a flattering number.
/// </remarks>
internal sealed class MeasuredByteSource(IByteSource inner) : IByteSource
{
    private readonly IByteSource _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>Calls that reached the inner source. Against a live process, the syscall count.</summary>
    public long Reads { get; private set; }

    /// <summary>Bytes requested from the inner source.</summary>
    public long Bytes { get; private set; }

    /// <summary>Reads the inner source refused.</summary>
    public long Failures { get; private set; }

    /// <inheritdoc/>
    public bool Is64Bit => _inner.Is64Bit;

    /// <inheritdoc/>
    public bool TryRead(ulong address, Span<byte> buffer)
    {
        Reads++;
        Bytes += buffer.Length;

        bool ok = _inner.TryRead(address, buffer);
        if (!ok)
        {
            Failures++;
        }

        return ok;
    }

    /// <summary>Zeroes the counters, for a warm-up pass that must not be reported.</summary>
    public void Reset()
    {
        Reads = 0;
        Bytes = 0;
        Failures = 0;
    }
}
