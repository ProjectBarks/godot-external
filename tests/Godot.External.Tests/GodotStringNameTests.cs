using Godot.External.Abi;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// docs/analysis.md §4.6 / §12.3b: <c>node + 0x1c0</c> → <c>StringName::_Data</c>, <c>_Data + 8</c>
/// → UTF-32 buffer.
/// </summary>
public class GodotStringNameTests
{
    private const ulong Node = 0x40000;
    private const ulong Data = 0x41000;
    private const ulong Buffer = 0x42000;

    private static FakeByteSource BuildNode(string name, GodotAbiProfile? profile = null)
    {
        GodotOffsetTable offsets = (profile ?? GodotAbiProfiles.Godot451Release).Offsets;

        return new FakeByteSource()
            .WritePointer(Node + (ulong)offsets.NodeName, Data)
            .WritePointer(Data + (ulong)offsets.StringNameDataToBuffer, Buffer)
            .WriteGodotString(Buffer, name, sizeBackOffset: offsets.CowDataSizeBackOffset);
    }

    [Fact]
    public void NodeName_ResolvesThroughBothIndirections()
    {
        FakeByteSource source = BuildNode("FmodBankLoader");

        Assert.True(GodotStringName.TryReadNodeName(source, GodotAbiProfiles.Godot451Release, Node, out string name));
        Assert.Equal("FmodBankLoader", name);
    }

    [Fact]
    public void NodeName_DecodesNonAscii()
    {
        FakeByteSource source = BuildNode("Étiquette-日本");

        Assert.True(GodotStringName.TryReadNodeName(source, GodotAbiProfiles.Godot451Release, Node, out string name));
        Assert.Equal("Étiquette-日本", name);
    }

    [Fact]
    public void NullData_IsAnEmptyName_NotAFailure()
    {
        FakeByteSource source = new FakeByteSource()
            .WritePointer(Node + (ulong)GodotAbiProfiles.Godot451Release.Offsets.NodeName, 0);

        Assert.True(GodotStringName.TryReadNodeName(source, GodotAbiProfiles.Godot451Release, Node, out string name));
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void UnreadableDataPointer_Fails()
    {
        FakeByteSource source = new(); // nothing mapped at all

        Assert.False(GodotStringName.TryReadNodeName(source, GodotAbiProfiles.Godot451Release, Node, out string name));
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void DebugProfileOffsets_ResolveTheSameStructure()
    {
        // The mechanism does not change between templates — only the offsets do (§4.6).
        GodotAbiProfile debug = GodotAbiProfiles.Godot451DebugUnvalidated;
        FakeByteSource source = BuildNode("Proxy", debug);

        Assert.True(GodotStringName.TryReadNodeName(source, debug, Node, out string name));
        Assert.Equal("Proxy", name);
    }

    [Fact]
    public void CalibratedStringNameOffset_ChangesTheReadPath()
    {
        // §12.5's calibrator overrides offsets and diffs against the table; an offset that is
        // settable but ignored at read time would silently make that diff meaningless.
        GodotAbiProfile calibrated = GodotAbiProfiles.Godot451Release
            .WithCalibratedOffset(GodotField.StringNameDataToBuffer, 0x10);

        FakeByteSource source = BuildNode("AudioManager", calibrated);

        Assert.True(GodotStringName.TryReadNodeName(source, calibrated, Node, out string name));
        Assert.Equal("AudioManager", name);

        // The shipped +0x08 now points at unmapped memory, so the uncalibrated profile must fail
        // rather than accidentally still working.
        Assert.False(GodotStringName.TryReadNodeName(source, GodotAbiProfiles.Godot451Release, Node, out _));
    }

    [Fact]
    public void CalibratedCowDataSizeOffset_ChangesTheReadPath()
    {
        GodotAbiProfile calibrated = GodotAbiProfiles.Godot451Release
            .WithCalibratedOffset(GodotField.CowDataSizeBackOffset, 0x10);

        FakeByteSource source = BuildNode("Necrobinder", calibrated);

        Assert.True(GodotStringName.TryReadNodeName(source, calibrated, Node, out string name));
        Assert.Equal("Necrobinder", name);

        Assert.False(GodotStringName.TryReadNodeName(source, GodotAbiProfiles.Godot451Release, Node, out _));
    }
}
