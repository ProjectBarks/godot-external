using Godot.External.Abi;

namespace Godot.External.Memory;

/// <summary>
/// An <see cref="IByteSource"/> that freezes what it has read, so a traversal sees one memory image
/// rather than a sequence of independently-sampled moments.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface exists to make the freeze visible.</b> A cache that only implemented
/// <see cref="IByteSource"/> would be indistinguishable from the live target at the call site, and
/// this project has twice paid for exactly that: docs/analysis.md §6.4 (a page cache inside a
/// snapshot silently cancelling agree-twice) and the calibrator's two-readings check, where the
/// repeat reading was served from bytes the cache already held and so was never a second reading.
/// Code that performs a temporal check can — and in this library does — ask
/// <see cref="IsCoherent"/> and refuse to pretend.
/// </para>
/// <para>
/// <b>Failure is frozen too.</b> A block that could not be read stays unreadable for the lifetime of
/// the instance. A page that becomes readable halfway through a traversal would otherwise
/// reintroduce the mixed-moment image the cache exists to prevent (LiveClr's <c>PageCache</c> makes
/// the same argument, and it is the half of that design most easily dropped by accident).
/// </para>
/// <para>Not thread-safe. A snapshot is single-threaded by construction.</para>
/// </remarks>
internal interface ICoherentByteSource : IByteSource, IDisposable
{
    /// <summary>
    /// <see langword="true"/> when repeated reads of one address are guaranteed to return the same
    /// bytes. Always <see langword="true"/> for the caches in this namespace; the property exists so
    /// callers can ask an arbitrary <see cref="IByteSource"/> without a type test of their own.
    /// </summary>
    bool IsCoherent { get; }

    /// <summary>What this source has done so far.</summary>
    CacheStatistics Statistics { get; }

    /// <summary>
    /// Tells the source that <paramref name="baseAddress"/> is the base of a Godot object, so a
    /// span-granular cache can fetch the whole struct on first touch instead of a field at a time.
    /// </summary>
    /// <remarks>
    /// Free and non-blocking: it records an address, it does not read. A cache with no use for the
    /// hint ignores it. Callers must never depend on it having happened — every implementation
    /// answers correctly without it, only slower.
    /// </remarks>
    void RegisterObject(ulong baseAddress);

    /// <summary>
    /// Records that a caller skipped a temporal check because this source is coherent. Purely a
    /// counter; see <see cref="CacheStatistics.AgreeTwiceSuppressed"/>.
    /// </summary>
    void NoteAgreeTwiceSuppressed();

    /// <summary>
    /// Drops every retained byte, positive and negative, so subsequent reads observe the target
    /// again.
    /// </summary>
    /// <remarks>
    /// <b>This breaks the one-image guarantee, deliberately.</b> Reads either side of an
    /// <see cref="Invalidate"/> come from two different moments and may not be consistent with each
    /// other. Prefer ending the snapshot and opening a new one, which also resets the age clock and
    /// the statistics.
    /// </remarks>
    void Invalidate();
}
