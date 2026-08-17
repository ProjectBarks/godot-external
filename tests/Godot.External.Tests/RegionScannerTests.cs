using System.Runtime.InteropServices;
using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Target;
using LiveClr.Memory;

namespace Godot.External.Tests;

/// <summary>
/// The whole-process scan, against a real process: this one.
/// </summary>
/// <remarks>
/// Nothing else in this suite exercises <c>VirtualQueryEx</c> region walking or chunked reads, and
/// they are the two pieces that cannot be checked against a synthetic image. Running them against
/// the test process itself is cheap and catches the failure that would otherwise look exactly like
/// "the calibrator could not solve this build": a region walk that silently yields nothing.
/// </remarks>
public sealed class RegionScannerTests
{
    [Fact]
    public void FindsAUtf32NeedleAndAPointerToItInThisProcess()
    {
        // A name the scan can look for, in the encoding a Godot StringName stores it in.
        byte[] buffer = GodotText.Utf32Needle("GodotExternalCalibratorScanProbe");
        GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        // ...and a slot holding its address, which is what step two of the root scan looks for.
        ulong[] slot = [(ulong)pinnedBuffer.AddrOfPinnedObject()];
        GCHandle pinnedSlot = GCHandle.Alloc(slot, GCHandleType.Pinned);

        try
        {
            Assert.True(WindowsProcessMemory.TryOpen(Environment.ProcessId, out WindowsProcessMemory? memory));
            using (memory)
            {
                ProcessRegionSource source = new(memory);
                Assert.NotEmpty(source.Regions());

                RegionScanner scanner = new(memory, source);

                IReadOnlyList<ulong> hits = scanner.FindBytes(buffer, limit: 8);
                Assert.Contains((ulong)pinnedBuffer.AddrOfPinnedObject(), hits);

                IReadOnlyDictionary<ulong, List<ulong>> pointers = scanner.FindPointersTo(slot, limit: 64);
                Assert.True(pointers.ContainsKey(slot[0]));
                Assert.Contains((ulong)pinnedSlot.AddrOfPinnedObject(), pointers[slot[0]]);
            }
        }
        finally
        {
            pinnedSlot.Free();
            pinnedBuffer.Free();
        }
    }
}
