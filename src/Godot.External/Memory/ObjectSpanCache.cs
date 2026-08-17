using Godot.External.Abi;

namespace Godot.External.Memory;

/// <summary>
/// A coherent cache whose <b>fetch granularity is a Godot object, not a page</b>: on first touch of
/// a registered node, one read pulls the whole struct, and every field of that node is served from
/// it thereafter.
/// </summary>
/// <remarks>
/// <para>
/// <b>This design was measured against a plain page cache and lost. It is not the default.</b> The
/// hypothesis was sound: the access pattern is a pointer chase with high locality <em>within</em> a
/// node and unknown locality <em>between</em> nodes, and reading one node touches seven or more
/// fields inside a span of about 1.2 KB. Godot's allocator turned out to put only 1.76 nodes in a
/// 4 KiB page on a real game, which does make a 4 KiB fetch a poor unit — but the fix is a
/// <em>smaller</em> block, not an object-shaped one. The fields this library reads cluster into a
/// few 128-byte windows (<c>0x148</c>/<c>0x1c0</c> for a walk, <c>0x370</c> and
/// <c>0x470..0x4c8</c> for geometry); a small aligned block fetches only the clusters a workload
/// uses, while a span fetches all of them every time. See <c>bench/README.md</c> for the table.
/// </para>
/// <para>
/// It stays in the tree because it is the evidence for that conclusion, because a caller who really
/// does read most of a node may still want it, and because the interval bookkeeping below is what
/// makes the comparison honest rather than a strawman.
/// </para>
/// <para>
/// <b>The store is uniform, the fetch is not.</b> Bytes are retained in fixed
/// <see cref="BlockSize"/> blocks, so two different fetch policies can never produce two overlapping
/// images of one address: a block that already exists is never replaced, and whichever fetch
/// materialised it first is the moment every later read of it observes. Only the <em>size and
/// alignment of the read issued on a miss</em> depends on whether the address belongs to a
/// registered object.
/// </para>
/// <para>
/// <b>An over-read that fails is not evidence of unreadable memory.</b> A node near the end of a
/// heap region has a span that runs past the mapping; the read of that span fails even though every
/// byte the caller wanted is present. Caching that failure would invent unmapped memory and make the
/// cached path lose reads the uncached path would have served — a silent, data-dependent divergence.
/// So a failed span fetch is retried at the minimum width the caller actually asked for, and only
/// <em>that</em> failure is remembered. Deliberate negative caching still applies to it, and to
/// page-policy fetches, for the reason LiveClr's cache gives: a page that becomes readable
/// mid-traversal would reintroduce the mixed-moment image the cache exists to prevent.
/// </para>
/// <para>Not thread-safe. A snapshot is single-threaded by construction.</para>
/// </remarks>
internal sealed class ObjectSpanCache : ICoherentByteSource
{
    /// <summary>
    /// Retention granularity. 128 bytes is well under the ~1.2 KB node span, so block alignment adds
    /// at most a few percent to a span fetch, and well over the 8 bytes of a pointer, so the
    /// bookkeeping stays proportionate.
    /// </summary>
    public const int DefaultBlockSize = 128;

    /// <summary>Windows decides readability per 4 KiB page; failures are probed at that width.</summary>
    private const int ProtectionGranularity = 4096;

    private const int BucketShift = 12;

    private readonly IByteSource _inner;
    private readonly Dictionary<ulong, Chunk> _blocks = [];
    private readonly Dictionary<ulong, List<ulong>> _buckets = [];
    private readonly HashSet<ulong> _bases = [];
    private readonly ulong _blockMask;
    private readonly ulong _pageMask;
    private readonly int _bucketLookback;
    private readonly bool _useSpans;
    private readonly bool _usePages;

    private long _logicalReads;
    private long _logicalBytes;
    private long _hits;
    private long _misses;
    private long _fetches;
    private long _fetchedBytes;
    private long _spanFetches;
    private long _spanOverreads;
    private long _blockFetches;
    private long _negativeEntries;
    private long _agreeTwiceSuppressed;
    private bool _disposed;

    /// <summary>Wraps <paramref name="inner"/>, which this cache never disposes.</summary>
    /// <param name="inner">The source misses are fetched from.</param>
    /// <param name="mode">
    /// <see cref="MemoryCacheMode.Span"/> fetches object spans and, for anything else, exactly what
    /// was asked for. <see cref="MemoryCacheMode.Hybrid"/> fetches object spans for registered
    /// objects and page-aligned blocks for everything else — link nodes, <c>StringName::_Data</c>,
    /// character buffers. <see cref="MemoryCacheMode.Page"/> uses page policy throughout, which makes
    /// this a page cache with a finer retention unit.
    /// </param>
    /// <param name="spanBytes">
    /// Object span. Should be the highest offset the profile can address plus that field's width;
    /// <see cref="SpanBytesFor"/> computes it.
    /// </param>
    /// <param name="pageSize">Fetch width for the page policy; a power of two.</param>
    /// <param name="blockSize">Retention granularity; a power of two no larger than the page size.</param>
    public ObjectSpanCache(
        IByteSource inner,
        MemoryCacheMode mode,
        int spanBytes,
        int pageSize = SnapshotPageCache.DefaultPageSize,
        int blockSize = DefaultBlockSize)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spanBytes);
        if (mode is not (MemoryCacheMode.Span or MemoryCacheMode.Hybrid or MemoryCacheMode.Page))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a cached mode.");
        }

        if (blockSize < 8 || (blockSize & (blockSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize, "Block size must be a power of two of at least 8.");
        }

        if (pageSize < blockSize || pageSize > SnapshotPageCache.MaxPageSize || (pageSize & (pageSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize), pageSize, $"Page size must be a power of two between {blockSize} and {SnapshotPageCache.MaxPageSize}.");
        }

        _inner = inner;
        Mode = mode;
        SpanBytes = spanBytes;
        PageSize = pageSize;
        BlockSize = blockSize;
        _blockMask = ~(ulong)(uint)(blockSize - 1);
        _pageMask = ~(ulong)(uint)(pageSize - 1);
        _bucketLookback = (spanBytes >> BucketShift) + 1;
        _useSpans = mode is MemoryCacheMode.Span or MemoryCacheMode.Hybrid;
        _usePages = mode is MemoryCacheMode.Page or MemoryCacheMode.Hybrid;
    }

    /// <summary>Fetch policy in force.</summary>
    public MemoryCacheMode Mode { get; }

    /// <summary>Object span in bytes.</summary>
    public int SpanBytes { get; }

    /// <summary>Fetch width for the page policy.</summary>
    public int PageSize { get; }

    /// <summary>Retention granularity.</summary>
    public int BlockSize { get; }

    /// <summary>Registered object bases.</summary>
    public int RegisteredObjects => _bases.Count;

    /// <inheritdoc/>
    public bool IsCoherent => true;

    /// <inheritdoc/>
    public bool Is64Bit => _inner.Is64Bit;

    /// <inheritdoc/>
    public CacheStatistics Statistics => new()
    {
        LogicalReads = _logicalReads,
        LogicalBytes = _logicalBytes,
        Hits = _hits,
        Misses = _misses,
        Fetches = _fetches,
        FetchedBytes = _fetchedBytes,
        SpanFetches = _spanFetches,
        SpanOverreads = _spanOverreads,
        BlockFetches = _blockFetches,
        NegativeEntries = _negativeEntries,
        AgreeTwiceSuppressed = _agreeTwiceSuppressed,
        RetainedEntries = _blocks.Count,
        RetainedBytes = (long)_blocks.Count * BlockSize,
    };

    /// <summary>
    /// The span this library will ever read from one object: the highest offset in
    /// <paramref name="offsets"/> that is measured from an object base, plus the width of the field
    /// there.
    /// </summary>
    /// <param name="offsets">The profile's offsets.</param>
    /// <param name="realSize">Width of one <c>real_t</c>; <c>offset[4]</c> is four of them.</param>
    /// <param name="includeText">
    /// Whether to cover <c>Label::text</c> and <c>RichTextLabel::text</c>. On the validated release
    /// profile those sit at <c>0x800</c> and <c>0xa78</c> against <c>0x4c0</c> for
    /// <c>size_cache</c>, so including them more than doubles the span for every node in order to
    /// serve the minority that are labels.
    /// </param>
    /// <remarks>
    /// Link-node, <c>StringName</c>, <c>CowData</c> and <c>ScriptInstance</c> offsets are excluded on
    /// purpose: they are measured from <em>other</em> allocations, not from the node, so including
    /// them would size the node span against something that is not in it.
    /// </remarks>
    public static int SpanBytesFor(GodotOffsetTable offsets, int realSize, bool includeText = false)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(realSize);

        int span = 0;
        Extend(offsets.CanvasItemVisible, 1);
        Extend(offsets.ControlGlobalPosition, 2 * realSize);
        Extend(offsets.ControlOffsets, 4 * realSize);
        Extend(offsets.ControlScale, 2 * realSize);
        Extend(offsets.ControlPosition, 2 * realSize);
        Extend(offsets.ControlSize, 2 * realSize);
        Extend(offsets.NodeParent, ByteSourceExtensions.PointerWidth);
        Extend(offsets.NodeChildListHead, ByteSourceExtensions.PointerWidth);
        Extend(offsets.NodeName, ByteSourceExtensions.PointerWidth);
        Extend(offsets.NodeScriptInstance, ByteSourceExtensions.PointerWidth);

        if (includeText)
        {
            Extend(offsets.LabelText, ByteSourceExtensions.PointerWidth);
            Extend(offsets.RichTextLabelText, ByteSourceExtensions.PointerWidth);
        }

        return span;

        void Extend(int offset, int width)
        {
            if (offset >= 0 && offset + width > span)
            {
                span = offset + width;
            }
        }
    }

    /// <inheritdoc/>
    public void RegisterObject(ulong baseAddress)
    {
        if (!_useSpans || baseAddress == 0 || _disposed || !_bases.Add(baseAddress))
        {
            return;
        }

        ulong bucket = baseAddress >> BucketShift;
        if (!_buckets.TryGetValue(bucket, out List<ulong>? list))
        {
            list = [];
            _buckets[bucket] = list;
        }

        list.Add(baseAddress);
    }

    /// <inheritdoc/>
    public void NoteAgreeTwiceSuppressed() => _agreeTwiceSuppressed++;

    /// <inheritdoc/>
    public bool TryRead(ulong address, Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logicalReads++;
        _logicalBytes += buffer.Length;

        if (buffer.Length == 0)
        {
            return true;
        }

        if (address == 0 || ulong.MaxValue - address < (ulong)(buffer.Length - 1))
        {
            buffer.Clear();
            return false;
        }

        ulong first = address & _blockMask;
        ulong last = (address + (ulong)(buffer.Length - 1)) & _blockMask;

        bool complete = true;
        for (ulong b = first; ; b += (ulong)BlockSize)
        {
            if (_blocks.ContainsKey(b))
            {
                _hits++;
            }
            else
            {
                _misses++;
                complete = false;
            }

            if (b == last)
            {
                break;
            }
        }

        if (!complete)
        {
            Materialise(address, buffer.Length);
        }

        int copied = 0;
        for (ulong b = first; ; b += (ulong)BlockSize)
        {
            if (!_blocks.TryGetValue(b, out Chunk chunk) || chunk.Buffer is null)
            {
                buffer.Clear();
                return false;
            }

            ulong from = b > address ? b : address;
            int within = (int)(from - b);
            int take = (int)Math.Min((ulong)(BlockSize - within), (ulong)buffer.Length - (ulong)copied);
            chunk.Buffer.AsSpan(chunk.Offset + within, take).CopyTo(buffer.Slice(copied, take));
            copied += take;

            if (b == last)
            {
                break;
            }
        }

        return copied == buffer.Length;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _blocks.Clear();
        _negativeEntries = 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _blocks.Clear();
        _buckets.Clear();
        _bases.Clear();
    }

    private void Materialise(ulong address, int length)
    {
        if (_useSpans && TryFindObjectBase(address, length, out ulong objectBase))
        {
            ulong start = objectBase & _blockMask;
            ulong end = AlignUp(objectBase + (ulong)SpanBytes, (ulong)BlockSize);
            if (Fetch(start, (int)(end - start), span: true))
            {
                return;
            }

            // The span over-read failed. That says nothing about the bytes the caller asked for, so
            // narrow to exactly those and let the honest failure be the one that is remembered.
            _spanOverreads++;
        }

        if (_usePages)
        {
            ulong pageStart = address & _pageMask;
            ulong pageEnd = AlignUp(address + (ulong)length, (ulong)PageSize);
            if (Fetch(pageStart, (int)(pageEnd - pageStart), span: false))
            {
                return;
            }

            return; // Fetch has already recorded the negative blocks it could prove.
        }

        ulong exactStart = address & _blockMask;
        ulong exactEnd = AlignUp(address + (ulong)length, (ulong)BlockSize);
        Fetch(exactStart, (int)(exactEnd - exactStart), span: false);
    }

    private bool Fetch(ulong start, int length, bool span)
    {
        byte[] buffer = new byte[length];

        _fetches++;
        _fetchedBytes += length;
        if (span)
        {
            _spanFetches++;
        }
        else
        {
            _blockFetches++;
        }

        if (_inner.TryRead(start, buffer))
        {
            Store(start, buffer, 0, length, readable: true);
            return true;
        }

        if (span)
        {
            return false; // never negative-cache an over-read; see the class remarks
        }

        if (length <= ProtectionGranularity)
        {
            Store(start, buffer, 0, length, readable: false);
            return false;
        }

        // A fetch wider than the protection granularity can straddle a boundary. Probe per 4 KiB so
        // one unmapped neighbour does not make readable pages look unreadable.
        bool any = false;
        for (int i = 0; i < length; i += ProtectionGranularity)
        {
            int width = Math.Min(ProtectionGranularity, length - i);
            Span<byte> slice = buffer.AsSpan(i, width);

            _fetches++;
            _fetchedBytes += width;
            _blockFetches++;
            bool ok = _inner.TryRead(start + (ulong)i, slice);
            if (!ok)
            {
                slice.Clear();
            }

            Store(start + (ulong)i, buffer, i, width, ok);
            any |= ok;
        }

        return any;
    }

    private void Store(ulong start, byte[] buffer, int offset, int length, bool readable)
    {
        for (int i = 0; i < length; i += BlockSize)
        {
            ulong blockBase = start + (ulong)i;

            // Existing blocks win. Two fetch policies can cover one address; the moment that address
            // was first observed is the moment every later read of it must observe.
            if (_blocks.ContainsKey(blockBase))
            {
                continue;
            }

            if (readable)
            {
                _blocks[blockBase] = new Chunk(buffer, offset + i);
            }
            else
            {
                _blocks[blockBase] = Chunk.Unreadable;
                _negativeEntries++;
            }
        }
    }

    private bool TryFindObjectBase(ulong address, int length, out ulong objectBase)
    {
        objectBase = 0;
        bool found = false;

        ulong bucket = address >> BucketShift;
        for (int k = 0; k <= _bucketLookback; k++)
        {
            if (bucket < (ulong)k)
            {
                break;
            }

            if (!_buckets.TryGetValue(bucket - (ulong)k, out List<ulong>? list))
            {
                continue;
            }

            foreach (ulong candidate in list)
            {
                if (candidate > address || address - candidate + (ulong)length > (ulong)SpanBytes)
                {
                    continue;
                }

                if (!found || candidate > objectBase)
                {
                    objectBase = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    private static ulong AlignUp(ulong value, ulong alignment) => (value + alignment - 1) & ~(alignment - 1);

    /// <param name="Buffer">The fetch this block came from, or null when the block is unreadable.</param>
    /// <param name="Offset">Where this block starts inside <paramref name="Buffer"/>.</param>
    private readonly record struct Chunk(byte[]? Buffer, int Offset)
    {
        public static Chunk Unreadable => new(null, 0);
    }
}
