using LiveClr.Memory;

namespace Godot.External.Calibrator.Target;

/// <summary>A committed, readable span of the target's address space.</summary>
public readonly record struct ScanRegion(ulong BaseAddress, ulong Size)
{
    /// <summary>One past the last byte.</summary>
    public ulong EndAddress => BaseAddress + Size;
}

/// <summary>Where a scan gets its list of regions. An interface so a scan can be tested offline.</summary>
public interface IRegionSource
{
    /// <summary>Every region worth scanning, ascending.</summary>
    IEnumerable<ScanRegion> Regions();
}

/// <summary>
/// Whole-process search — the bootstrap of last resort, and the only one a GDScript cell has.
/// </summary>
/// <remarks>
/// <para>
/// Everything else in this calibrator starts from a node address. On a .NET cell that address can be
/// reached through the CLR; on a GDScript cell there is no managed side at all, and the harness
/// supplies no pointers by design. What it does supply is every node <em>name</em>, and a Godot
/// <c>StringName</c> stores its characters as UTF-32 — which is a 44-byte needle for
/// <c>"RootHarness"</c>, distinctive enough to find the scene from nothing.
/// </para>
/// <para>
/// Scanning is bounded and reports when it stopped early, because a truncated scan that silently
/// returns fewer hits would feed a wrong-but-unique answer into everything downstream.
/// </para>
/// </remarks>
public sealed class RegionScanner(IMemoryReader reader, IRegionSource regions)
{
    /// <summary>Read granularity. Large enough that syscall overhead is not the cost.</summary>
    public const int ChunkBytes = 1 << 20;

    /// <summary>Bytes examined before a scan gives up.</summary>
    public const long DefaultBudgetBytes = 3L << 30;

    private readonly IMemoryReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    private readonly IRegionSource _regions = regions ?? throw new ArgumentNullException(nameof(regions));

    /// <summary>True when the last scan stopped on its budget rather than at the end of memory.</summary>
    public bool LastScanTruncated { get; private set; }

    /// <summary>Addresses at which <paramref name="needle"/> occurs.</summary>
    public IReadOnlyList<ulong> FindBytes(ReadOnlyMemory<byte> needle, int limit, long budget = DefaultBudgetBytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(needle.Length);

        List<ulong> hits = [];
        Scan(needle.Length - 1, budget, (address, span) =>
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

    /// <summary>Addresses of 8-aligned slots holding one of <paramref name="values"/>.</summary>
    public IReadOnlyDictionary<ulong, List<ulong>> FindPointersTo(
        IReadOnlyCollection<ulong> values,
        int limit,
        long budget = DefaultBudgetBytes)
    {
        ArgumentNullException.ThrowIfNull(values);

        HashSet<ulong> wanted = [.. values];
        Dictionary<ulong, List<ulong>> hits = [];
        int found = 0;

        Scan(7, budget, (address, span) =>
        {
            int start = (int)((8 - (address % 8)) % 8);
            for (int i = start; i + 8 <= span.Length; i += 8)
            {
                ulong value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(span[i..]);
                if (value == 0 || !wanted.Contains(value))
                {
                    continue;
                }

                if (!hits.TryGetValue(value, out List<ulong>? slots))
                {
                    slots = [];
                    hits[value] = slots;
                }

                ulong slot = address + (ulong)i;
                if (slots.Contains(slot))
                {
                    continue; // chunks overlap so a needle can straddle a boundary
                }

                slots.Add(slot);
                if (++found >= limit)
                {
                    return false;
                }
            }

            return true;
        });

        return hits;
    }

    private void Scan(int overlap, long budget, Func<ulong, ReadOnlySpan<byte>, bool> onChunk)
    {
        LastScanTruncated = false;
        byte[] buffer = new byte[ChunkBytes];
        long examined = 0;

        foreach (ScanRegion region in _regions.Regions())
        {
            ulong address = region.BaseAddress;
            while (address < region.EndAddress)
            {
                if (examined >= budget)
                {
                    LastScanTruncated = true;
                    return;
                }

                int length = (int)Math.Min((ulong)ChunkBytes, region.EndAddress - address);
                if (_reader.TryRead(address, buffer.AsSpan(0, length)))
                {
                    examined += length;
                    if (!onChunk(address, buffer.AsSpan(0, length)))
                    {
                        return;
                    }
                }

                if (length <= overlap)
                {
                    break;
                }

                address += (ulong)(length - overlap);
            }
        }
    }
}
