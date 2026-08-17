using Godot.External.Calibrator.Calibration;

namespace Godot.External.Tests;

/// <summary>
/// The free corroboration Godot hands out: it names its own internal children after their class.
/// </summary>
/// <remarks>
/// <c>@VScrollBar@2</c> is a <c>VScrollBar</c>, so wherever the walk contains one of these the node
/// name confirms the derived class name with no scene knowledge whatsoever. It is the one
/// independent check available on a build where the class map is otherwise taken on the strength of
/// a partition and a decode.
/// </remarks>
public sealed class ClassNameCorroborationTests
{
    [Fact]
    public void AnInternalNodeNameConfirmsItsClass()
    {
        Dictionary<ulong, string> classes = new() { [1] = "VScrollBar", [2] = "Label" };
        Dictionary<ulong, string> names = new() { [1] = "@VScrollBar@2", [2] = "ZetaLabelAscii" };

        Assert.True(ClassNameCalibrator.Corroborated(classes, names));
    }

    [Fact]
    public void AnInternalNodeNameContradictingItsClassIsNotCorroboration()
    {
        Dictionary<ulong, string> classes = new() { [1] = "Panel", [2] = "Label" };
        Dictionary<ulong, string> names = new() { [1] = "@VScrollBar@2", [2] = "ZetaLabelAscii" };

        // The one node that could have confirmed the map says something else, so this candidate has
        // no independent support at all.
        Assert.False(ClassNameCalibrator.Corroborated(classes, names));
    }

    [Fact]
    public void AWalkWithNoInternalNodesIsAcceptedOnTheOtherEvidence()
    {
        Dictionary<ulong, string> classes = new() { [1] = "Control", [2] = "Label" };
        Dictionary<ulong, string> names = new() { [1] = "RootHarness", [2] = "ZetaLabelAscii" };

        // Nothing to corroborate against is not the same as failing to corroborate; uniqueness across
        // offsets stays the real gate.
        Assert.True(ClassNameCalibrator.Corroborated(classes, names));
    }

    [Theory]
    [InlineData("Label", true)]
    [InlineData("RichTextLabel", true)]
    [InlineData("VScrollBar", true)]
    [InlineData("", false)]
    [InlineData("9Lives", false)]
    [InlineData("res://Probe.gd", false)]
    [InlineData("C:/WINDOWS/FONTS/seguisym.ttf", false)]
    public void OnlyIdentifiersAreAcceptedAsClassNames(string candidate, bool expected)
        => Assert.Equal(expected, ClassNameCalibrator.IsClassIdentifier(candidate));
}
