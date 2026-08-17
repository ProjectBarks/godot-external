using System.Diagnostics;
using Godot.External.Abi;

namespace Godot.External.Memory;

/// <summary>
/// A scoped, coherent view of the target's memory. While one is open, every read the owning
/// <see cref="Scene.SceneEpoch"/> performs is served from a frozen image; when it is disposed the
/// epoch goes back to reading the live target.
/// </summary>
/// <remarks>
/// <para>
/// <b>The scope is the invalidation.</b> This library has twice been burnt by a cache that never
/// invalidated: docs/analysis.md §6.4, where a page cache inside a snapshot cancelled the
/// agree-twice check it was supposed to complement, and the calibrator's two-readings check, where
/// the second reading was served from bytes already held and so was never a second reading. The
/// lesson taken from both is that <em>an invalidation you have to remember is one you will
/// forget</em>. So:
/// </para>
/// <list type="number">
/// <item>
/// Caching is off by default. An epoch with no snapshot open reads the target, one syscall at a
/// time, exactly as before this type existed.
/// </item>
/// <item>
/// A snapshot must be disposed to release the epoch, and <see cref="Scene.SceneEpoch.Snapshot"/>
/// refuses to open a second one while the first is live. Leaking one across a poll therefore breaks
/// the <em>next</em> poll loudly instead of the current one silently.
/// </item>
/// <item>
/// Reads taken past <see cref="MemoryCacheOptions.MaxAge"/> set <see cref="IsStale"/> and are
/// counted in <see cref="CacheStatistics.StaleReads"/>. The snapshot does not refresh itself:
/// swapping the image out mid-traversal is the failure this type exists to prevent, so it reports
/// rather than repairs.
/// </item>
/// <item>
/// Code inside the library that performs a temporal check asks <see cref="IsCoherent"/> and declines
/// to run a check that a frozen image has already decided — see
/// <see cref="CacheStatistics.AgreeTwiceSuppressed"/>. That turns §6.4's "the two mitigations
/// cancel" from a documented hazard into an observable counter.
/// </item>
/// </list>
/// <para>
/// <b>How a caller can still get this wrong</b> is stated plainly in the README of <c>bench/</c> and
/// worth repeating: hold one snapshot across several polls and every poll returns the first poll's
/// data. Nothing here can prevent that — only <see cref="IsStale"/> and the read counters make it
/// visible. The narrow scope, one-at-a-time rule, and age reporting make it awkward; they do not
/// make it impossible.
/// </para>
/// </remarks>
internal sealed class MemorySnapshot : ICoherentByteSource
{
    private static int _nextSequence;

    private readonly IByteSource _raw;
    private readonly ICoherentByteSource? _cache;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Action _onDispose;
    private readonly HashSet<ulong>? _seenAddresses;

    private long _uncachedReads;
    private long _uncachedBytes;
    private long _repeatedReads;
    private long _staleReads;
    private long _uncachedAgreeTwiceSuppressed;
    private bool _disposed;

    internal MemorySnapshot(IByteSource raw, GodotAbiProfile profile, MemoryCacheOptions options, Action onDispose)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(onDispose);

        _raw = raw;
        _onDispose = onDispose;
        Options = options;
        Sequence = Interlocked.Increment(ref _nextSequence);

        int spanBytes = options.SpanBytes > 0
            ? options.SpanBytes
            : ObjectSpanCache.SpanBytesFor(profile.Offsets, profile.RealSize, options.SpanIncludesText);

        _cache = options.Mode switch
        {
            MemoryCacheMode.None => null,
            MemoryCacheMode.Page => new SnapshotPageCache(raw, options.PageSize),
            MemoryCacheMode.Span or MemoryCacheMode.Hybrid =>
                new ObjectSpanCache(raw, options.Mode, spanBytes, options.PageSize),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown cache mode."),
        };

        SpanBytes = spanBytes;

        if (options.DetectRepeatedReads)
        {
            _seenAddresses = [];
        }
    }

    /// <summary>Monotonic identifier, unique within the process. Useful when two snapshots are confused.</summary>
    public int Sequence { get; }

    /// <summary>The configuration this snapshot was opened with.</summary>
    public MemoryCacheOptions Options { get; }

    /// <summary>The object span in force, whether supplied or derived from the profile.</summary>
    public int SpanBytes { get; }

    /// <summary>How long this snapshot has been open.</summary>
    public TimeSpan Age => _clock.Elapsed;

    /// <summary>
    /// <see langword="true"/> once <see cref="Age"/> exceeds <see cref="MemoryCacheOptions.MaxAge"/>.
    /// Reads still succeed — see the remarks on this class for why it reports rather than repairs.
    /// </summary>
    public bool IsStale => _cache is not null && Age > Options.MaxAge;

    /// <inheritdoc/>
    /// <remarks>
    /// <see langword="false"/> for <see cref="MemoryCacheMode.None"/>, which is the point of that
    /// mode: it exercises the snapshot API without freezing anything, so temporal checks keep running.
    /// </remarks>
    public bool IsCoherent => _cache?.IsCoherent ?? false;

    /// <inheritdoc/>
    public bool Is64Bit => _raw.Is64Bit;

    /// <inheritdoc/>
    public CacheStatistics Statistics
    {
        get
        {
            CacheStatistics inner = _cache?.Statistics ?? new CacheStatistics
            {
                LogicalReads = _uncachedReads,
                LogicalBytes = _uncachedBytes,
                Fetches = _uncachedReads,
                FetchedBytes = _uncachedBytes,
                AgreeTwiceSuppressed = _uncachedAgreeTwiceSuppressed,
            };

            return inner with
            {
                RepeatedReads = _repeatedReads,
                StaleReads = _staleReads,
            };
        }
    }

    /// <inheritdoc/>
    public bool TryRead(ulong address, Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_seenAddresses is not null && buffer.Length > 0 && !_seenAddresses.Add(address))
        {
            _repeatedReads++;
        }

        if (IsStale)
        {
            _staleReads++;
        }

        if (_cache is not null)
        {
            return _cache.TryRead(address, buffer);
        }

        _uncachedReads++;
        _uncachedBytes += buffer.Length;
        return _raw.TryRead(address, buffer);
    }

    /// <inheritdoc/>
    public void RegisterObject(ulong baseAddress) => _cache?.RegisterObject(baseAddress);

    /// <inheritdoc/>
    public void NoteAgreeTwiceSuppressed()
    {
        if (_cache is null)
        {
            _uncachedAgreeTwiceSuppressed++;
        }
        else
        {
            _cache.NoteAgreeTwiceSuppressed();
        }
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cache?.Invalidate();
        _seenAddresses?.Clear();
        _clock.Restart();
    }

    /// <summary>Ends the snapshot and returns the epoch to reading the live target.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cache?.Dispose();
        _onDispose();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        CacheStatistics stats = Statistics;
        return $"MemorySnapshot #{Sequence} ({Options.Mode}, {Age.TotalMilliseconds:F1} ms"
             + $"{(IsStale ? ", STALE" : string.Empty)}, {stats.Fetches} fetches, "
             + $"{stats.Amplification:F2}x amplification)";
    }
}
