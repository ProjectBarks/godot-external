using Godot.External.Abi;

namespace Godot.External.Memory;

/// <summary>
/// A read-through cache of page-aligned blocks. The shape LiveClr's <c>PageCache</c> uses, ported to
/// this library's two-member <see cref="IByteSource"/> seam.
/// </summary>
/// <remarks>
/// <para>
/// Every block is fetched at most once for the lifetime of the instance, so one traversal sees one
/// memory image. That is the point; the speed is a side effect, and a large one — on a real game a
/// 4 Hz subtree poll fell from 105,740 syscalls and 71 ms to 5,980 and 11 ms.
/// </para>
/// <para>
/// <b>The block size matters more here than it does in LiveClr.</b> A Godot <c>Control</c> is
/// roughly 1.3 KB and Godot's allocator does not lay siblings out in walk order, so a 4 KiB page
/// holds 1.76 nodes on a measured target — enough that page-granularity works, nowhere near enough
/// to make 4 KiB the right width. <see cref="MemoryCacheOptions.PageSize"/> defaults to 512 for
/// that reason, and <c>bench/README.md</c> has the sweep behind it.
/// </para>
/// <para>
/// <b>Failures are cached.</b> A block that could not be read stays unreadable for this instance's
/// lifetime, because a page that becomes readable mid-traversal would reintroduce exactly the
/// mixed-moment image the cache exists to prevent.
/// </para>
/// <para>
/// <b>Lifetime is the contract.</b> Construct one per <see cref="MemorySnapshot"/>, dispose it with
/// the snapshot, and never share one across polls. A long-lived cache does not go stale in a way it
/// can detect — it silently keeps serving a moment that has passed.
/// </para>
/// </remarks>
internal sealed class SnapshotPageCache : ICoherentByteSource
{
    /// <summary>4 KiB — the x64 page size, and the granularity Windows applies protections at.</summary>
    public const int DefaultPageSize = 4096;

    /// <summary>Largest permitted block. Beyond this a "cache" is just a speculative bulk read.</summary>
    public const int MaxPageSize = 1 << 20;

    /// <summary>
    /// Protection granularity. Whether a byte is readable is decided per 4 KiB page, so a block
    /// larger than this can span readable and unreadable pages and must be probed per page.
    /// </summary>
    private const int ProtectionGranularity = 4096;

    private readonly IByteSource _inner;
    private readonly ulong _blockMask;
    private readonly Dictionary<ulong, Block> _blocks = [];

    private long _logicalReads;
    private long _logicalBytes;
    private long _hits;
    private long _misses;
    private long _fetches;
    private long _fetchedBytes;
    private long _negativeEntries;
    private long _agreeTwiceSuppressed;
    private bool _disposed;

    /// <summary>Wraps <paramref name="inner"/>, which this cache never disposes.</summary>
    /// <param name="inner">The source uncached blocks are fetched from.</param>
    /// <param name="pageSize">Block size; a power of two between 8 bytes and 1 MiB.</param>
    public SnapshotPageCache(IByteSource inner, int pageSize = DefaultPageSize)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (pageSize < 8 || pageSize > MaxPageSize || (pageSize & (pageSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize), pageSize, $"Page size must be a power of two between 8 and {MaxPageSize}.");
        }

        _inner = inner;
        PageSize = pageSize;
        _blockMask = ~(ulong)(uint)(pageSize - 1);
    }

    /// <summary>Block size in bytes.</summary>
    public int PageSize { get; }

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
        BlockFetches = _fetches,
        NegativeEntries = _negativeEntries,
        AgreeTwiceSuppressed = _agreeTwiceSuppressed,
        RetainedEntries = _blocks.Count,
        RetainedBytes = RetainedBytes(),
    };

    /// <summary>
    /// No-op. A page cache has no use for object extents: its blocks are decided by the address, not
    /// by what lives there.
    /// </summary>
    public void RegisterObject(ulong baseAddress)
    {
        // Intentionally empty; see the summary.
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

        int copied = 0;
        ulong cursor = address;

        while (copied < buffer.Length)
        {
            ulong blockBase = cursor & _blockMask;
            int offset = (int)(cursor - blockBase);
            int take = Math.Min(PageSize - offset, buffer.Length - copied);

            Block block = GetOrFetch(blockBase);
            if (block.Data is null || !IsRangeValid(block, offset, take))
            {
                // Fail closed and discard the prefix already copied: a caller that ignores the
                // return value must not see a partly-filled buffer that happens to look like a
                // valid object (docs/analysis.md §4.8).
                buffer.Clear();
                return false;
            }

            block.Data.AsSpan(offset, take).CopyTo(buffer.Slice(copied, take));
            copied += take;
            cursor += (ulong)take;
        }

        return true;
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
    }

    private long RetainedBytes()
    {
        long total = 0;
        foreach (Block block in _blocks.Values)
        {
            if (block.Data is not null)
            {
                total += block.Data.Length;
            }
        }

        return total;
    }

    private Block GetOrFetch(ulong blockBase)
    {
        if (_blocks.TryGetValue(blockBase, out Block? cached))
        {
            _hits++;
            return cached;
        }

        _misses++;
        Block block = Fetch(blockBase);
        _blocks[blockBase] = block;
        if (block.Data is null)
        {
            _negativeEntries++;
        }

        return block;
    }

    private Block Fetch(ulong blockBase)
    {
        byte[] data = new byte[PageSize];

        _fetches++;
        _fetchedBytes += PageSize;
        if (_inner.TryRead(blockBase, data))
        {
            return new Block(data, null);
        }

        // With the default block size this is the end of it: Windows decides readability per 4 KiB
        // page, so an aligned 4 KiB read is all-or-nothing and a failure means the page is not there.
        if (PageSize <= ProtectionGranularity)
        {
            return Block.Unreadable;
        }

        // Larger blocks can straddle a region boundary. Re-read per protection page so one unmapped
        // neighbour does not make readable pages look unreadable — otherwise raising PageSize would
        // lose reads the default size would have served.
        bool[] valid = new bool[PageSize / ProtectionGranularity];
        bool any = false;
        for (int i = 0; i < valid.Length; i++)
        {
            Span<byte> slice = data.AsSpan(i * ProtectionGranularity, ProtectionGranularity);

            _fetches++;
            _fetchedBytes += ProtectionGranularity;
            if (_inner.TryRead(blockBase + (ulong)(i * ProtectionGranularity), slice))
            {
                valid[i] = true;
                any = true;
            }
            else
            {
                slice.Clear();
            }
        }

        return any ? new Block(data, valid) : Block.Unreadable;
    }

    private static bool IsRangeValid(Block block, int offset, int length)
    {
        if (block.SubPageValid is null)
        {
            return true;
        }

        int first = offset / ProtectionGranularity;
        int last = (offset + length - 1) / ProtectionGranularity;
        for (int i = first; i <= last; i++)
        {
            if (!block.SubPageValid[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <param name="Data">Block contents, or null when nothing in the block is readable.</param>
    /// <param name="SubPageValid">Null when the whole block is valid — the common case, and allocation-free.</param>
    private sealed record Block(byte[]? Data, bool[]? SubPageValid)
    {
        public static Block Unreadable { get; } = new(null, null);
    }
}
