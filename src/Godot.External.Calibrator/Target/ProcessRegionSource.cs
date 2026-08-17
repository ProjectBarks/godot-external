using LiveClr.Memory;

namespace Godot.External.Calibrator.Target;

/// <summary>
/// The target's committed private read/write regions, walked with <c>VirtualQueryEx</c> through
/// LiveClr's <see cref="WindowsProcessMemory.TryQueryRegion"/>.
/// </summary>
/// <remarks>
/// Restricted to <c>MEM_PRIVATE</c> and writable protections: Godot's scene tree, its
/// <c>StringName</c> table and every <c>CowData</c> buffer live on the heap, while mapped images and
/// file-backed sections are megabytes of ground that cannot hold a live node. The filter is a
/// performance decision, not a correctness one — anything it excludes is stated here rather than
/// discovered later as a mysterious miss.
/// </remarks>
public sealed class ProcessRegionSource(WindowsProcessMemory memory) : IRegionSource
{
    private const uint MemPrivate = 0x20000;
    private const uint PageReadWrite = 0x04;
    private const uint PageWriteCopy = 0x08;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint PageExecuteWriteCopy = 0x80;
    private const uint PageGuard = 0x100;
    private const uint PageNoAccess = 0x01;

    private const ulong UserSpaceEnd = 0x7FFF_FFFE_FFFFUL;

    private readonly WindowsProcessMemory _memory = memory ?? throw new ArgumentNullException(nameof(memory));

    /// <inheritdoc/>
    public IEnumerable<ScanRegion> Regions()
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
                yield return new ScanRegion(region.BaseAddress, region.Size);
            }

            ulong next = region.EndAddress;
            if (next <= address)
            {
                yield break; // no forward progress: stop rather than spin
            }

            address = next;
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
