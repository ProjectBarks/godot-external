using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;

namespace Godot.External.Tests;

/// <summary>
/// Class identity from the vtable, which needs no calibration and no version knowledge.
/// </summary>
/// <remarks>
/// This replaced reading <c>Object::_class_name_ptr</c>, which could never have worked on 4.2–4.4:
/// <c>_postinitialize()</c> assigns that field and nulls it again two lines later, so it reads zero
/// for the whole observable life of every node. Three rounds of fallbacks, thresholds and
/// corroboration against it produced no change at all, because there was nothing there.
/// </remarks>
public sealed class RttiClassIdentityTests
{
    [Theory]
    [InlineData("5Label", "Label")]
    [InlineData("13RichTextLabel", "RichTextLabel")]
    [InlineData("7Control", "Control")]
    [InlineData("*5Label", "Label")]
    public void ALengthPrefixedNameDecodes(string mangled, string expected)
    {
        Assert.True(ClassNameCalibrator.TryDemangle(mangled, out string name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("N3ns05LabelE")]   // nested/namespaced: not a Godot node class
    [InlineData("5Label junk")]    // trailing qualifier
    [InlineData("Label")]          // no length prefix
    [InlineData("99Label")]        // prefix disagrees with the payload
    [InlineData("")]
    public void AnythingElseIsRefusedRatherThanGuessed(string mangled)
        => Assert.False(ClassNameCalibrator.TryDemangle(mangled, out _));

    [Fact]
    public void ClassesComeFromTheVtableWithNoOffsetDerivedAtAll()
    {
        GridSceneMemory memory = new();
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        Assert.Equal("Label", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").NodeClass);
        Assert.Equal("RichTextLabel", result.Nodes.Single(n => n.Name == "ZetaRich").NodeClass);
        Assert.Equal("Node", result.Nodes.Single(n => n.Name == "DeltaSiblingOne").NodeClass);
        Assert.All(result.Nodes, n => Assert.Equal("engine", n.ClassSource));
    }

    [Fact]
    public void TheScriptInstanceNamesItsOwnImplementingClass()
    {
        GridSceneMemory memory = new();
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // scriptInstance.ownerBackref is a member of CSharpInstance or GDScriptInstance — unrelated
        // classes implementing one interface — and NOT of the engine object, so no single value is
        // right for both. The axis is not the cell's binding either: a mono template runs .gd scripts
        // perfectly well, so one process holds both kinds and the answer differs per node. Reading
        // the class off the ScriptInstance's own vtable is the only way to ask the real question.
        Assert.Equal("CSharpInstance", result.Derivation.Walk.ScriptInstanceClass);
    }

    [Fact]
    public void TheHierarchyComesFromTheTargetNotFromAListOfGodotClassNames()
    {
        GridSceneMemory memory = new();
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // Label -> Control -> CanvasItem, walked through __si_class_type_info's __base_type at +16.
        // Knowing the NAME "Label" is not enough to answer "does this node have a CanvasItem?", and
        // hardcoding an answer per class name would fail on any custom class a real game defines.
        NodeRecord label = result.Nodes.Single(n => n.Name == "ZetaLabelAscii");
        Assert.Equal("Label", label.NodeClass);
        Assert.NotNull(label.Visible);
        Assert.NotNull(label.Size);

        // Node -> Object: no CanvasItem anywhere in the chain, so it has no visibility to report,
        // whatever the bytes at those offsets happen to say.
        NodeRecord bare = result.Nodes.Single(n => n.Name == "DeltaSiblingOne");
        Assert.Equal("Node", bare.NodeClass);
        Assert.Null(bare.Visible);
        Assert.Null(bare.Size);
    }

    [Fact]
    public void ANonItaniumTypeInfoIsDeclinedRatherThanDecodedAsGarbage()
    {
        GridSceneMemory memory = new(msvcRtti: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // Official Windows templates are MinGW-GCC, so Itanium is the right shape — but another
        // toolchain puts something else where type_info's vtable would be, and reading one as the
        // other yields a plausible-looking wrong answer. Declining is the only safe response, and
        // with no class identity there is no text either.
        Assert.All(result.Nodes, n => Assert.Null(n.ClassSource));
        Assert.All(result.Nodes, n => Assert.Null(n.NodeClass));
        Assert.All(result.Nodes, n => Assert.Null(n.Text));
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.LabelText));

        // The note reports what was MEASURED — the shape is wrong — and does not name a toolchain the
        // probe cannot actually identify.
        Assert.Contains(result.Notes, n => n.Contains("not shaped like Itanium RTTI", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Notes, n => n.Contains("MSVC-built", StringComparison.Ordinal));
    }
}
