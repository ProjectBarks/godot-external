using System.Buffers.Binary;
using System.IO.Compression;
using Godot.External.Abi;

namespace Godot.External.Bench.Fixtures;

/// <summary>
/// A sparse, page-granular image of a target's address space, used as both the recorded fixture and
/// the synthetic heap.
/// </summary>
/// <remarks>
/// <para>
/// Sparse at 4 KiB, because that is where the fidelity has to be: Windows decides readability per
/// page, so a fixture that answered every address would let a cached variant "succeed" on reads the
/// live target refuses, and the whole negative-caching argument would go untested. A read that
/// touches one unmapped page fails entirely, exactly as <c>ReadProcessMemory</c> does.
/// </para>
/// <para>
/// This is docs/analysis.md §8.8's "recorded-fixture provider", narrowed to what the benchmark
/// needs: replay a real game's memory in CI so the syscall and amplification numbers are the same
/// numbers, byte for byte and address for address, that the live run produced.
/// </para>
/// </remarks>
internal sealed class MemoryImage : IByteSource
{
    /// <summary>Page size. Matches the x64 page and the granularity protections are applied at.</summary>
    public const int PageSize = 4096;

    private const ulong PageMask = ~(ulong)(PageSize - 1);
    private const ulong Magic = 0x31484347_5845_4F47UL; // "GOEXGCH1"

    private readonly Dictionary<ulong, byte[]> _pages = [];

    /// <inheritdoc/>
    public bool Is64Bit => true;

    /// <summary>Pages held.</summary>
    public int MappedPages => _pages.Count;

    /// <summary>Bytes held.</summary>
    public long MappedBytes => (long)_pages.Count * PageSize;

    /// <inheritdoc/>
    public bool TryRead(ulong address, Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return true;
        }

        if (address == 0 || ulong.MaxValue - address < (ulong)(buffer.Length - 1))
        {
            return false;
        }

        int copied = 0;
        ulong cursor = address;

        while (copied < buffer.Length)
        {
            ulong page = cursor & PageMask;
            if (!_pages.TryGetValue(page, out byte[]? data))
            {
                return false;
            }

            int offset = (int)(cursor - page);
            int take = Math.Min(PageSize - offset, buffer.Length - copied);
            data.AsSpan(offset, take).CopyTo(buffer.Slice(copied, take));
            copied += take;
            cursor += (ulong)take;
        }

        return true;
    }

    /// <summary>Whether the page holding <paramref name="address"/> is present.</summary>
    public bool HasPage(ulong address) => _pages.ContainsKey(address & PageMask);

    /// <summary>Maps the page holding <paramref name="address"/> if it is not already mapped.</summary>
    public byte[] EnsurePage(ulong address)
    {
        ulong page = address & PageMask;
        if (!_pages.TryGetValue(page, out byte[]? data))
        {
            data = new byte[PageSize];
            _pages[page] = data;
        }

        return data;
    }

    /// <summary>Writes <paramref name="data"/> at <paramref name="address"/>, mapping pages as needed.</summary>
    public void Write(ulong address, ReadOnlySpan<byte> data)
    {
        int written = 0;
        while (written < data.Length)
        {
            ulong cursor = address + (ulong)written;
            byte[] page = EnsurePage(cursor);
            int offset = (int)(cursor & (PageSize - 1));
            int take = Math.Min(PageSize - offset, data.Length - written);
            data.Slice(written, take).CopyTo(page.AsSpan(offset, take));
            written += take;
        }
    }

    /// <summary>Writes a 64-bit little-endian value.</summary>
    public void WriteUInt64(ulong address, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        Write(address, buffer);
    }

    /// <summary>Writes a 32-bit little-endian value.</summary>
    public void WriteUInt32(ulong address, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        Write(address, buffer);
    }

    /// <summary>Writes a single-precision <c>real_t</c>.</summary>
    public void WriteSingle(ulong address, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        Write(address, buffer);
    }

    /// <summary>
    /// Copies the page holding <paramref name="address"/> out of <paramref name="source"/>, if it is
    /// not already present. Returns whether the page is now mapped.
    /// </summary>
    /// <remarks>
    /// A page the source refuses is left <em>absent</em> rather than recorded as zeroes: the replay
    /// must fail those reads the same way the live target did, or the recorded run and the live run
    /// stop being the same experiment.
    /// </remarks>
    public bool CapturePage(IByteSource source, ulong address)
    {
        ArgumentNullException.ThrowIfNull(source);

        ulong page = address & PageMask;
        if (_pages.ContainsKey(page))
        {
            return true;
        }

        byte[] data = new byte[PageSize];
        if (!source.TryRead(page, data))
        {
            return false;
        }

        _pages[page] = data;
        return true;
    }

    /// <summary>
    /// Writes the image to <paramref name="path"/>, deflate-compressed, with the three addresses the
    /// workloads start from.
    /// </summary>
    /// <remarks>
    /// The anchors travel <em>inside</em> the fixture on purpose. They are meaningless without these
    /// exact bytes — a native <c>Node*</c> from one process is not a node in another (§8.8) — and a
    /// fixture that could be paired with the wrong anchors would fail in the most confusing way
    /// available.
    /// </remarks>
    public void Save(string path, ulong root, ulong node, ulong subtree)
    {
        using FileStream file = File.Create(path);
        using DeflateStream deflate = new(file, CompressionLevel.Optimal);
        using BinaryWriter writer = new(deflate);

        writer.Write(Magic);
        writer.Write(PageSize);
        writer.Write(root);
        writer.Write(node);
        writer.Write(subtree);
        writer.Write(_pages.Count);

        foreach ((ulong page, byte[] data) in _pages.OrderBy(p => p.Key))
        {
            writer.Write(page);
            writer.Write(data);
        }
    }

    /// <summary>Reads an image written by <see cref="Save"/>, and the anchors stored with it.</summary>
    public static MemoryImage Load(string path, out ulong root, out ulong node, out ulong subtree)
    {
        using FileStream file = File.OpenRead(path);
        using DeflateStream deflate = new(file, CompressionMode.Decompress);
        using BinaryReader reader = new(deflate);

        if (reader.ReadUInt64() != Magic)
        {
            throw new InvalidDataException($"{path} is not a Godot.External benchmark fixture.");
        }

        int pageSize = reader.ReadInt32();
        if (pageSize != PageSize)
        {
            throw new InvalidDataException($"{path} has page size {pageSize}; this build expects {PageSize}.");
        }

        root = reader.ReadUInt64();
        node = reader.ReadUInt64();
        subtree = reader.ReadUInt64();

        int count = reader.ReadInt32();
        MemoryImage image = new();
        for (int i = 0; i < count; i++)
        {
            ulong page = reader.ReadUInt64();
            image._pages[page] = reader.ReadBytes(pageSize);
        }

        return image;
    }
}
