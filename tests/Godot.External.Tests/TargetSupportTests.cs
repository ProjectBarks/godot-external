using Godot.External.Abi;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// docs/analysis.md §4.1 concluded the target is x64-only and the pointer width should be hardcoded
/// rather than carrying a speculative 32-bit path. Every read path therefore <b>refuses</b> a
/// non-64-bit source instead of guessing a layout no profile describes and no offset was recovered
/// from. These tests are what keeps that refusal real.
/// </summary>
public class TargetSupportTests
{
    private const ulong Node = 0x90000;
    private const ulong Buffer = 0x91000;

    private static GodotAbiProfile Profile => GodotAbiProfiles.Godot451Release;

    private static FakeByteSource Build32Bit()
    {
        // Fully populated as if it were readable: the refusal must come from the target width, not
        // from an incidentally failing read.
        FakeByteSource source = new() { Is64Bit = false };
        GodotOffsetTable offsets = Profile.Offsets;

        source.WriteGodotString(Buffer, "Proxy");
        source.WritePointer(Node + (ulong)offsets.LabelText, Buffer);
        source.WritePointer(Node + (ulong)offsets.NodeName, 0);
        source.WritePointer(Node + (ulong)offsets.NodeChildListHead, 0);
        source.WriteSingle(Node + (ulong)offsets.ControlPosition, 12f);
        source.WriteSingle(Node + (ulong)offsets.ControlPosition + 4, 34f);
        source.WriteBytes(Node + (ulong)offsets.CanvasItemVisible, [1]);

        return source;
    }

    [Fact]
    public void PointerReads_AreRefused()
    {
        Assert.False(Build32Bit().TryReadPointer(Node, out ulong value));
        Assert.Equal(0ul, value);
    }

    [Fact]
    public void CowDataReads_AreRefused()
    {
        Assert.False(CowData.TryReadElementCount(Build32Bit(), Buffer, out _));
        Assert.False(CowData.TryReadBlock(Build32Bit(), Buffer, sizeof(uint), out _));
    }

    [Fact]
    public void StringReads_AreRefused()
    {
        Assert.False(GodotString.TryRead(Build32Bit(), Buffer, out _));
        Assert.False(GodotString.TryReadField(Build32Bit(), Profile, Node, GodotField.LabelText, out _));
    }

    [Fact]
    public void StringNameReads_AreRefused()
    {
        // Note this would otherwise SUCCEED with an empty name (the _Data pointer is null), so the
        // refusal is doing real work here rather than riding on a failed read.
        Assert.False(GodotStringName.TryReadNodeName(Build32Bit(), Profile, Node, out _));
    }

    [Fact]
    public void ChildWalk_IsRefused()
    {
        ChildWalkResult result = ChildListWalk.Walk(Build32Bit(), Profile, Node);

        Assert.Equal(ChildWalkStatus.ReadFailed, result.Status);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void GeometryReads_AreRefused()
    {
        FakeByteSource source = Build32Bit();

        Assert.False(ControlGeometry.TryReadVector2(source, Profile, Node, GodotField.ControlPosition, out _));
        Assert.False(ControlGeometry.TryReadVisible(source, Profile, Node, out _));
        Assert.False(ControlGeometry.TryComposeGlobalPosition(source, Profile, Node, out _, out _, _ => true));

        Span<double> offsets = stackalloc double[4];
        Assert.False(ControlGeometry.TryReadOffsets(source, Profile, Node, offsets));
    }

    [Fact]
    public void TheSameFixtureReadsFineOn64Bit()
    {
        // Proves the fixtures above are otherwise valid, so the refusals are attributable to Is64Bit.
        FakeByteSource source = new() { Is64Bit = true };
        source.WriteGodotString(Buffer, "Proxy");
        source.WritePointer(Node + (ulong)Profile.Offsets.LabelText, Buffer);

        Assert.True(GodotString.TryReadField(source, Profile, Node, GodotField.LabelText, out string text));
        Assert.Equal("Proxy", text);
    }
}
