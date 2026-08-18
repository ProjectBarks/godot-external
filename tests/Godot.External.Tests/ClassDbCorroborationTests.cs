using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;
using Godot.External.Calibrator.Reflection;

namespace Godot.External.Tests;

/// <summary>
/// The wiring around the live corroboration route — the parts that can be judged without a running
/// Godot process.
/// </summary>
/// <remarks>
/// The route itself is validated where it has to be, against live export templates: docs/analysis.md
/// §16 and the T5b run. What is asserted here is everything that decides <em>whether the route runs
/// at all and against what</em>, because those are the failures that look like a considered refusal
/// and are in fact a bug — the version parse below reported "Godot 0.x is unsupported" on every live
/// grid cell, which reads exactly like the version gate doing its job.
/// </remarks>
public sealed class ClassDbCorroborationTests
{
    [Theory]
    // What the grid's own targets actually report. This one was the live defect.
    [InlineData("4.5-stable (official)", 4, 5)]
    [InlineData("4.3-stable (official)", 4, 3)]
    [InlineData("4.6.3-stable (official)", 4, 6)]
    // What Engine::get_version_info() and every doc example spell.
    [InlineData("4.4.1.stable.mono", 4, 4)]
    [InlineData("4.5.stable", 4, 5)]
    public void TheEngineVersionIsParsedInEveryShapeATargetReportsIt(string version, int major, int minor)
        => Assert.Equal((major, minor), ClassDbCorroborator.ParseEngineVersion(version));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("stable")]
    [InlineData("4")]
    [InlineData("vX.Y")]
    public void AnUnreadableVersionRefusesRatherThanGuesses(string? version)
    {
        // (0, 0) is what GodotReflectionSupport turns into a refusal. Guessing "probably 4.5" would
        // send a 4.6 target down the 4.5 walker, whose only symptom is that every name reads empty —
        // which looks like "this engine has no such class", not like a layout mistake.
        Assert.Equal((0, 0), ClassDbCorroborator.ParseEngineVersion(version));
    }

    [Fact]
    public void EveryProbeNamesAContractKeyTheCalibratorActuallyDerives()
    {
        // A probe for a key nothing derives would report noOpinion on every target forever, and a
        // list of them would read as coverage. The keys must be the same strings profiles.json and
        // lib/checks.mjs use, or the corroborated value is compared against nothing.
        HashSet<string> contract =
        [
            OffsetKeys.NodeParent, OffsetKeys.NodeName, OffsetKeys.CanvasItemVisible,
            OffsetKeys.ControlSize, OffsetKeys.ControlPosition, OffsetKeys.ControlScale,
            OffsetKeys.LabelText, OffsetKeys.RichTextLabelText,
        ];

        foreach ((string key, _, _) in ClassDbCorroborator.DefaultProbes)
        {
            Assert.Contains(key, contract);
        }
    }

    [Fact]
    public void NoKeyIsProbedTwice()
    {
        // Two probes for one key would publish two verdicts for it, and a reader taking the first
        // would be reading whichever the list happened to order first.
        string[] keys = [.. ClassDbCorroborator.DefaultProbes.Select(p => p.Key)];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryProbedMethodIsBoundByTheClassThatProbesIt()
    {
        // ClassInfo::method_map holds only a class's OWN registrations, so a probe naming an
        // inherited method resolves to nothing on every target — measured: Label::is_visible,
        // Label::get_parent and Control::get_text all find no bind (§16.4). That is a refusal that
        // looks identical to a structural limit, so the pairing is pinned here instead.
        (string Key, string Class, string Method)[] expected =
        [
            ("canvasItem.visible", "CanvasItem", "is_visible"),
            ("control.size", "Control", "get_size"),
            ("control.position", "Control", "get_position"),
            ("control.scale", "Control", "get_scale"),
            ("node.parent", "Node", "get_parent"),
            ("node.name", "Node", "get_name"),
            ("label.text", "Label", "get_text"),
            ("richTextLabel.text", "RichTextLabel", "get_text"),
        ];

        Assert.Equal<IEnumerable<(string, string, string)>>(expected, ClassDbCorroborator.DefaultProbes);
    }

    [Fact]
    public void TheVerdictSpellingsAreTheOnesTheHarnessCompares()
    {
        // lib/checks.mjs matches these as literals. A rename on either side turns every verdict into
        // "not one of agree/disagree/noOpinion/notCompared", which fails loudly — but only because
        // the harness enumerates them, so pin the strings here too.
        Assert.Equal("agree", CorroborationVerdicts.Agree);
        Assert.Equal("disagree", CorroborationVerdicts.Disagree);
        Assert.Equal("noOpinion", CorroborationVerdicts.NoOpinion);
        Assert.Equal("notCompared", CorroborationVerdicts.NotCompared);
        Assert.Equal("classdb-getter-disassembly", CorroborationMethods.ClassDbGetter);
        Assert.Equal("ran", CorroborationStatuses.Ran);
    }

    [Fact]
    public void ASeedNeedsAMethodTheClassBindsItself()
    {
        // Panel and VScrollBar bind no methods of their own and identify 0 of 5 / 0 of 4 — a true
        // negative, and a wasted whole-process pass. Each seed here costs about 600 MB of scanning,
        // so the list is short and every entry has to earn its place.
        Assert.All(ClassDbSeed.DefaultSeeds, s =>
        {
            Assert.NotEmpty(s.Class);
            Assert.NotEmpty(s.Method);
        });

        // Label::get_text identified on all eight 4.3/4.5 cells and every pass, so it goes first: the
        // list stops at the first success and the order is therefore the cost.
        Assert.Equal(("Label", "get_text"), ClassDbSeed.DefaultSeeds[0]);
        Assert.DoesNotContain(ClassDbSeed.DefaultSeeds, s => s.Class is "Panel" or "VScrollBar");
    }

    [Fact]
    public void ACorroborationRecordDefaultsToNotComparedWithNoValue()
    {
        // The default must be the refusal, not the agreement. A record built by a future code path
        // that forgets to set its verdict has to read as "nothing was established".
        CorroborationRecord empty = new();

        Assert.Equal(CorroborationVerdicts.NotCompared, empty.Agreement);
        Assert.Null(empty.Offset);
        Assert.Null(empty.RecordClass);
        Assert.Null(empty.RecordMethod);
    }

    [Fact]
    public void AnUnrunCorroborationSectionSaysSoRatherThanLookingEmpty()
    {
        GetterCorroboration none = new();

        Assert.Equal(CorroborationStatuses.Unsupported, none.Status);
        Assert.Empty(none.Records);
    }
}
