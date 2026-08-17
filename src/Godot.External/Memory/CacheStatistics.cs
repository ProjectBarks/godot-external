namespace Godot.External.Memory;

/// <summary>
/// What a caching <see cref="Abi.IByteSource"/> decorator did, in numbers a caller can act on.
/// </summary>
/// <remarks>
/// <para>
/// The counter that matters most is <see cref="Amplification"/>. A page cache that serves an
/// eight-byte pointer out of a freshly fetched 4 KiB page has read 512&#215; the bytes the caller
/// asked for; whether that is a win depends entirely on how many of the other 4,088 bytes get used
/// before the snapshot ends. Nothing else in this record answers that question, and neither does a
/// hit rate — a hit rate counts <em>lookups</em>, not bytes.
/// </para>
/// <para>
/// <see cref="Hits"/> is also a temporal-safety signal, not only a performance one. A read served
/// from a hit observed a moment that has already passed. That is exactly what a snapshot is for, and
/// exactly what defeats a check written to observe change — see
/// <see cref="AgreeTwiceSuppressed"/> and docs/analysis.md §6.4.
/// </para>
/// </remarks>
public readonly record struct CacheStatistics
{
    /// <summary>Calls the library made to the cache.</summary>
    public long LogicalReads { get; init; }

    /// <summary>Bytes the library actually asked for — the denominator of <see cref="Amplification"/>.</summary>
    public long LogicalBytes { get; init; }

    /// <summary>Block or span lookups served from bytes already held.</summary>
    public long Hits { get; init; }

    /// <summary>Block or span lookups that had to go to the inner source.</summary>
    public long Misses { get; init; }

    /// <summary>Reads issued to the inner source — the syscall count, when the inner source is the OS.</summary>
    public long Fetches { get; init; }

    /// <summary>Bytes requested from the inner source — the numerator of <see cref="Amplification"/>.</summary>
    public long FetchedBytes { get; init; }

    /// <summary>Fetches that pulled a whole object span (<see cref="ObjectSpanCache"/>).</summary>
    public long SpanFetches { get; init; }

    /// <summary>
    /// Span fetches that failed because the span over-read past the end of a mapped region. These
    /// do <b>not</b> become negative cache entries: the caller's narrower read may well succeed, and
    /// caching the over-read's failure would invent unreadable memory. See
    /// <see cref="ObjectSpanCache"/>.
    /// </summary>
    public long SpanOverreads { get; init; }

    /// <summary>Fetches that pulled an aligned block (<see cref="SnapshotPageCache"/>).</summary>
    public long BlockFetches { get; init; }

    /// <summary>Blocks or spans that could not be read and are being replayed as failures.</summary>
    public long NegativeEntries { get; init; }

    /// <summary>
    /// Times a temporal check was suppressed because the source is coherent and re-reading could
    /// only return the same bytes. Non-zero means a mitigation the code still contains is no longer
    /// doing anything — see <see cref="ICoherentByteSource.NoteAgreeTwiceSuppressed"/>.
    /// </summary>
    public long AgreeTwiceSuppressed { get; init; }

    /// <summary>
    /// Logical reads of an address already read during this snapshot, when
    /// <see cref="MemoryCacheOptions.DetectRepeatedReads"/> is on. A caller re-reading an address to
    /// observe change gets a straight count of how many times it observed nothing instead.
    /// </summary>
    public long RepeatedReads { get; init; }

    /// <summary>Logical reads taken after the snapshot passed <see cref="MemoryCacheOptions.MaxAge"/>.</summary>
    public long StaleReads { get; init; }

    /// <summary>Distinct blocks and spans retained, readable or not.</summary>
    public int RetainedEntries { get; init; }

    /// <summary>Bytes of target memory currently held.</summary>
    public long RetainedBytes { get; init; }

    /// <summary>
    /// Bytes read from the target per byte the library asked for. 1.0 is perfect; a page cache on a
    /// pointer chase with no locality approaches <c>PageSize / 8</c>.
    /// </summary>
    public double Amplification => LogicalBytes == 0 ? 0 : (double)FetchedBytes / LogicalBytes;

    /// <summary>Fraction of block/span lookups served without touching the target.</summary>
    public double HitRate
    {
        get
        {
            long lookups = Hits + Misses;
            return lookups == 0 ? 0 : (double)Hits / lookups;
        }
    }

    /// <summary>Logical reads served per fetch — how many syscalls the cache saved, per syscall spent.</summary>
    public double ReadsPerFetch => Fetches == 0 ? 0 : (double)LogicalReads / Fetches;
}
