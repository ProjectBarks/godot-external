using LiveClr.Memory;

namespace Godot.External.Bench.Targets;

/// <summary>
/// The minimum whole-process search the benchmark needs to find a scene root: locate a byte pattern,
/// then locate the 8-aligned slots pointing at what it found.
/// </summary>
/// <remarks>
/// <para>
/// The calibrator has a fuller version of this (<c>RegionScanner</c>, with budgets and truncation
/// reporting). It is not reused, on purpose: the benchmark is not a calibration, it needs two
/// methods rather than twelve, and coupling a measurement harness to the project most likely to be
/// under active edit is how a benchmark stops being runnable.
/// </para>
/// <para>
/// Restricted to committed, writable, <c>MEM_PRIVATE</c> regions. Godot's scene tree, its
/// <c>StringName</c> table and every <c>CowData</c> buffer live on the heap; mapped images and
/// file-backed sections are megabytes of ground that cannot hold a live node.
/// </para>
/// </remarks>
internal sealed class ProcessScanner(WindowsProcessMemory memory)
{
    private const int ChunkBytes = 1 << 20;
    private const ulong UserSpaceEnd = 0x7FFF_FFFE_FFFFUL;

    private const uint MemPrivate = 0x20000;
    private const uint PageNoAccess = 0x01;
    private const uint PageReadWrite = 0x04;
    private const uint PageWriteCopy = 0x08;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint PageExecuteWriteCopy = 0x80;
    private const uint PageGuard = 0x100;

    private readonly WindowsProcessMemory _memory = memory ?? throw new ArgumentNullException(nameof(memory));

    /// <summary>Addresses at which <paramref name="needle"/> occurs, up to <paramref name="limit"/>.</summary>
    public IReadOnlyList<ulong> FindBytes(ReadOnlyMemory<byte> needle, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfZero(needle.Length);

        List<ulong> hits = [];
        Scan(needle.Length - 1, (address, span) =>
        {
            int from = 0;
            while (hits.Count < limit)
            {
                int at = span[from..].IndexOf(needle.Span);
                if (at < 0)
                {
                    break;
                }

                hits.Add(address + (ulong)(from + at));
                from += at + 1;
            }

            return hits.Count < limit;
        });

        return hits;
    }

    /// <summary>8-aligned slots holding one of <paramref name="values"/>, up to <paramref name="limit"/> in total.</summary>
    public IReadOnlyList<ulong> FindPointersTo(IReadOnlyCollection<ulong> values, int limit)
    {
        ArgumentNullException.ThrowIfNull(values);

        HashSet<ulong> wanted = [.. values];
        List<ulong> slots = [];

        Scan(7, (address, span) =>
        {
            int start = (int)((8 - (address % 8)) % 8);
            for (int i = start; i + 8 <= span.Length; i += 8)
            {
                ulong value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(span[i..]);
                if (value == 0 || !wanted.Contains(value))
                {
                    continue;
                }

                slots.Add(address + (ulong)i);
                if (slots.Count >= limit)
                {
                    return false;
                }
            }

            return true;
        });

        // Chunks overlap so a slot can be seen twice; ordering also makes the search deterministic
        // on an unchanging target, which matters when the answer feeds a benchmark.
        return [.. slots.Distinct().Order()];
    }

    private void Scan(int overlap, Func<ulong, ReadOnlySpan<byte>, bool> onChunk)
    {
        byte[] buffer = new byte[ChunkBytes];

        foreach ((ulong regionBase, ulong regionSize) in Regions())
        {
            ulong address = regionBase;
            ulong end = regionBase + regionSize;

            while (address < end)
            {
                int length = (int)Math.Min((ulong)ChunkBytes, end - address);
                if (_memory.TryRead(address, buffer.AsSpan(0, length)) && !onChunk(address, buffer.AsSpan(0, length)))
                {
                    return;
                }

                if (length <= overlap)
                {
                    break;
                }

                address += (ulong)(length - overlap);
            }
        }
    }

    private IEnumerable<(ulong Base, ulong Size)> Regions()
    {
        ulong address = 0x10000;
        while (address < UserSpaceEnd)
        {
            if (!_memory.TryQueryRegion(address, out MemoryRegion region) || region.Size == 0)
            {
                yield break;
            }

            if (IsScannable(region))
            {
                yield return (region.BaseAddress, region.Size);
            }

            if (region.EndAddress <= address)
            {
                yield break; // no forward progress: stop rather than spin
            }

            address = region.EndAddress;
        }
    }

    private static bool IsScannable(MemoryRegion region)
    {
        if (!region.IsCommitted || region.Type != MemPrivate)
        {
            return false;
        }

        uint protect = region.Protect & 0xFF;
        return (region.Protect & PageGuard) == 0
            && protect != PageNoAccess
            && protect is PageReadWrite or PageWriteCopy or PageExecuteReadWrite or PageExecuteWriteCopy;
    }
}
