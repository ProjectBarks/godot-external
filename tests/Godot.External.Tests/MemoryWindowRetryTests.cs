using Godot.External.Calibrator.Calibration;
using LiveClr.Memory;

namespace Godot.External.Tests;

/// <summary>
/// A momentary read failure must not leave a window permanently incomplete.
/// </summary>
/// <remarks>
/// An incomplete window is not merely missing data: every derivation that scanned it must then
/// withhold, because §12.5's coverage gate cannot tell a hole from a candidate that was never there.
/// One transient failure took a cell from 17 checks to 3 in a single run — <c>node.parent</c> had the
/// right candidates in hand and correctly refused to publish them. Retrying the holes costs one pass
/// and removes that class of event, without weakening the gate for a hole that is really there.
/// </remarks>
public sealed class MemoryWindowRetryTests
{
    private const ulong Origin = 0x10000;
    private const int Length = 0x100;
    private const ulong HoleAt = Origin + 0x40;

    [Fact]
    public void APermanentHoleStillLeavesTheWindowIncomplete()
    {
        FlakyReader reader = new(HoleAt, 8, int.MaxValue);
        MemoryWindow window = MemoryWindow.Read(reader, Origin, Length);

        // The gate must still fire for a hole that is really there — this is the half that must NOT
        // be weakened.
        Assert.False(window.Complete);
        Assert.False(window.IsReadable(0x40, 8));
        Assert.True(window.IsReadable(0, 0x40));
    }

    /// <summary>
    /// Attempts the initial bisecting read makes over an 8-byte hole in a 0x100 window, measured.
    /// </summary>
    /// <remarks>
    /// Pinned rather than recomputed. Deriving it inside the test made the test self-calibrating, so
    /// it passed identically with the retry removed — it measured whatever the code did and then
    /// agreed with it. <see cref="TheReadStrategyMakesTheAttemptsThisTestAssumes"/> is what keeps a
    /// pinned number honest when the read strategy changes.
    /// </remarks>
    private const int InitialReadAttempts = 6;

    [Fact]
    public void TheReadStrategyMakesTheAttemptsThisTestAssumes()
    {
        FlakyReader reader = new(HoleAt, 8, int.MaxValue);
        MemoryWindow.Read(reader, Origin, Length);

        // The bisecting read narrows to the granule, then the retry pass has one more go.
        Assert.Equal(InitialReadAttempts + 1, reader.Failures);
    }

    [Fact]
    public void AHoleThatHealsIsRecoveredByTheRetry()
    {
        // The flake outlasts the initial read exactly, and no more. Without the retry there is no
        // further attempt and the window stays incomplete — which is the whole difference.
        FlakyReader healing = new(HoleAt, 8, InitialReadAttempts);
        MemoryWindow window = MemoryWindow.Read(healing, Origin, Length);

        Assert.True(window.Complete);
        Assert.True(window.IsReadable(0, Length));
    }

    /// <summary>Fails reads that touch one range, for the first <c>failFirst</c> attempts only.</summary>
    private sealed class FlakyReader(ulong holeStart, int holeLength, int failFirst) : IMemoryReader
    {
        public int Failures { get; private set; }

        public bool Is64Bit => true;

        public bool TryRead(ulong address, Span<byte> buffer)
        {
            bool overlaps = address < holeStart + (ulong)holeLength && holeStart < address + (ulong)buffer.Length;
            if (overlaps && Failures < failFirst)
            {
                Failures++;
                return false;
            }

            buffer.Clear();
            return true;
        }

        public void Dispose()
        {
        }
    }
}
