using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;

namespace Godot.External.Tests;

/// <summary>
/// The property everything else in this calibrator rests on: where it is unsure, it says nothing.
/// </summary>
/// <remarks>
/// A missing offset costs one check. A confidently wrong one costs the credibility of every other
/// number in the table, because a reader has no way to tell which of them is the wrong kind. Each
/// test here reproduces a case where a wrong value actually reached a published result.
/// </remarks>
public sealed class AbsentNeverWrongTests
{
    private static int Offset(IReadOnlyDictionary<string, string> offsets, string key)
    {
        Assert.True(offsets.ContainsKey(key), $"{key} was not derived");
        return Convert.ToInt32(offsets[key][2..], 16);
    }

    private static DriverResult Calibrate(GridSceneMemory memory)
        => new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

    [Fact]
    public void NoTextIsPublishedWithoutAnOffsetBehindIt()
    {
        GridSceneMemory memory = new(phantomTextOnPlainNodes: true, secondRichTextLabel: true);
        DriverResult result = Calibrate(memory);

        // Two bracket-passing strings on two different text-less Controls, each forming its own
        // single-node group. Neither node set matches the one node the engine calls a
        // RichTextLabel, so both are discarded by class and the real field is left standing —
        // where before, the groups merely tied and every node covered by exactly one candidate was
        // handed its junk, "all candidates agree" being trivially true over a set of one.
        Assert.Null(result.Nodes.Single(n => n.Name == "EpsilonCore").Text);
        Assert.Null(result.Nodes.Single(n => n.Name == "AlphaLeaf").Text);
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", result.Nodes.Single(n => n.Name == "ZetaRich").Text);
    }

    [Fact]
    public void ARealControlDoesNotJoinTheLabelClass()
    {
        GridSceneMemory memory = new(labelTextPlusStrayControl: true);
        DriverResult result = Calibrate(memory);

        // The harder of the two shapes seen in the wild, and the reason geometry plausibility is not
        // enough: 0x8f8 decodes on both Labels AND on the walk root, which is a perfectly real
        // Control. Three of twenty is no majority, the string is a valid one, and the root reads as a
        // Control because it is one — so every earlier rule passes it. Only the engine's own class
        // name says the set is not the Labels.
        Assert.Null(result.Nodes.Single(n => n.Name == "RootHarness").Text);
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
    }

    [Fact]
    public void TheEngineNamesEveryNodesClass()
    {
        GridSceneMemory memory = new();
        DriverResult result = Calibrate(memory);

        // Object::_class_name_ptr -> a static StringName shared by every instance of the class. One
        // more indirection than node.name, which has been read correctly on every cell for nine
        // series, so the mechanism is not new — only the chain is.
        Assert.Equal("Label", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").NodeClass);
        Assert.Equal("RichTextLabel", result.Nodes.Single(n => n.Name == "ZetaRich").NodeClass);
        Assert.Equal("Node", result.Nodes.Single(n => n.Name == "DeltaSiblingOne").NodeClass);
        Assert.Equal("Control", result.Nodes.Single(n => n.Name == "RootHarness").NodeClass);
    }

    [Fact]
    public void OneUnreadableInstanceDoesNotDiscardTheClassOffset()
    {
        GridSceneMemory memory = new(oneLabelEmpty: true);
        DriverResult result = Calibrate(memory);

        // The decode-set is a strict SUBSET of the class-set: one Label has no text to decode. The
        // safety property is "nothing OUTSIDE the class decodes here", which a subset satisfies —
        // equality adds no safety and makes correctness hostage to every instance being readable at
        // the same instant. Demanding it discarded the correct, shipped-profile offset.
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
        Assert.Null(result.Nodes.Single(n => n.Name == "ZetaLabelUnicode").Text);
    }

    [Fact]
    public void WithoutClassNamesTextIsWithheldRatherThanGuessedFromGeometry()
    {
        GridSceneMemory memory = new(noClassNames: true);
        DriverResult result = Calibrate(memory);

        // Geometry cannot tell a Label from any other Control — the walk root IS a Control, and so is
        // a Label — so as a SUBSTITUTE for class identity it leaked every phantom of two consecutive
        // series while never once catching the case that matters.
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.LabelText));
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.RichTextLabelText));
        foreach (NodeRecord node in result.Nodes)
        {
            Assert.Null(node.Text);
        }

        // ...and no class is reported at all. The geometry inference could only ever say "Control" or
        // "Node", which is a different claim rather than a weaker one — and it is the claim text
        // publication is gated on.
        Assert.All(result.Nodes, n => Assert.Null(n.NodeClass));
        Assert.All(result.Nodes, n => Assert.Null(n.ClassSource));
    }

    [Fact]
    public void AClassReadFromTheEngineSaysSo()
    {
        GridSceneMemory memory = new();
        DriverResult result = Calibrate(memory);

        Assert.All(result.Nodes, n => Assert.Equal("engine", n.ClassSource));
    }

    [Fact]
    public void OneUnresolvableNodeDoesNotWithholdTheWholeClassMap()
    {
        GridSceneMemory memory = new(oneClassPointerMissing: true);
        DriverResult result = Calibrate(memory);

        // "Names EVERY walked node's class" is the same over-constraint that emptied the visible
        // bracket and let one node veto a derivation. One node that cannot answer is not evidence
        // against an offset that named all the others.
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));

        // The node that could not be named is reported as un-named, not guessed at.
        NodeRecord unnamed = result.Nodes.Single(n => n.Name == "OmegaChild");
        Assert.Null(unnamed.NodeClass);
        Assert.Null(unnamed.ClassSource);
        Assert.Equal("engine", result.Nodes.Single(n => n.Name == "ZetaRich").ClassSource);
    }

    [Fact]
    public void TheLabelBracketAssertsNothingAboutUnwrittenBytes()
    {
        GridSceneMemory memory = new();
        DriverResult result = Calibrate(memory);

        // The fixture writes 0x5a at Label::text+0x14, because that is what the real engine does: it
        // is a live member past xl_text, measured varying between two Labels in one process. A clause
        // requiring it to be zero made the reference cell's derivation a coin flip — one Label
        // happening to hold zero there was enough to publish under subset-not-equality — and it was
        // the third derivation in this calibrator broken by assuming C++ zeroes what nobody wrote.
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
        Assert.Equal("héllo ✦ 日本語", result.Nodes.Single(n => n.Name == "ZetaLabelUnicode").Text);
    }

    [Fact]
    public void AnUnsharedTranslationCopyIsDeclined()
    {
        GridSceneMemory memory = new(unsharedXlText: true);
        DriverResult result = Calibrate(memory);

        // text and xl_text hold bit-identical pointers in every sampled instance under both bindings.
        // Where they do not, this is not the pair the bracket describes, and declining is right — a
        // clause was once dropped on the theory that .NET breaks this sharing, and target memory says
        // otherwise.
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.LabelText));
        Assert.Null(result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
    }

    [Fact]
    public void AnUnconfirmedOwnerBackrefIsWithheldWhileTheSlotSurvives()
    {
        GridSceneMemory memory = new(scriptInstanceVanishes: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request(), null, memory.AdvanceFrame).Run(memory.Root);

        // The slot has a ceiling and, on a .NET cell, a managed bridge to corroborate it. The
        // back-reference has neither, and it published three different values across three runs of one
        // unchanged binary — so it is held to positive confirmation instead of inheriting the slot's
        // benefit of the doubt.
        Assert.Equal(GridSceneMemory.NodeScriptInstance, Offset(result.Derivation.Structural.Offsets, OffsetKeys.NodeScriptInstance));
        Assert.False(result.Derivation.Walk.Offsets.ContainsKey(OffsetKeys.ScriptInstanceOwner));
        Assert.Contains(result.Notes, n => n.Contains("ownerBackref", StringComparison.Ordinal));
    }

    [Fact]
    public void AWithheldParentOffsetMeansNoParentPointerIsReported()
    {
        GridSceneMemory memory = new(unreadableParentWindow: true);
        DriverResult result = Calibrate(memory);

        // The withhold itself is correct and is not being weakened: an unreadable stretch can drop the
        // true offset from one sample while a coincidence survives in the rest. What was wrong is that
        // the walk kept reporting parentPtr anyway — one run gave the same image-range address as
        // every node's parent, a published field with no derivation behind it.
        Assert.False(result.Derivation.Structural.Offsets.ContainsKey(OffsetKeys.NodeParent));
        Assert.All(result.Nodes, n => Assert.Null(n.ParentPtr));
    }

    [Fact]
    public void EveryOffsetTheDriverDoesNotDeriveIsDeclaredWithAReason()
    {
        GridSceneMemory memory = new();
        DriverResult result = Calibrate(memory);

        // Silence and a considered refusal are indistinguishable from outside, and a comparison
        // cannot interpret silence: a .NET driver that never derived scriptInstance.gcHandle — the
        // offset the whole managed bridge hangs off — scored identically to a correct one. The driver
        // is the only party that knows why a field is absent, so it is the party that has to say.
        Assert.Contains(OffsetKeys.ControlGlobalPosition, result.Derivation.NotDerived.Keys);
        Assert.Contains(OffsetKeys.ControlAnchor, result.Derivation.NotDerived.Keys);

        // No managed probe in this run, so the GCHandle has no chain to be found through — declared
        // rather than merely missing.
        Assert.Contains(OffsetKeys.ScriptInstanceGcHandle, result.Derivation.NotDerived.Keys);

        foreach ((string key, string reason) in result.Derivation.NotDerived)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"{key} was declined without a reason");
            Assert.False(result.Derivation.Semantic.Offsets.ContainsKey(key), $"{key} both declined and published");
            Assert.False(result.Derivation.Walk.Offsets.ContainsKey(key), $"{key} both declined and published");
        }
    }

    [Fact]
    public void AnEvenSplitOnOneNodeAlsoWithholds()
    {
        GridSceneMemory memory = new(richTextTie: true);
        DriverResult result = Calibrate(memory);

        // The other bail-out: one class, two candidates, one vote each, nothing to prefer between
        // them. Both paths must return an empty pool, not the pool they were weighing.
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.RichTextLabelText));
        Assert.Null(result.Nodes.Single(n => n.Name == "ZetaRich").Text);
    }

    [Fact]
    public void AnEvenSplitDeclaresTheTieRatherThanGoingSilent()
    {
        // secondRichTextLabel is load-bearing, not decoration. With the authored scene's single
        // RichTextLabel, OfClass refuses the key before the candidates are ever weighed ("exactly ONE
        // node of class RichTextLabel"), so the even-split branch never runs and a test pointed at
        // this fixture alone would be green without exercising it — which is what
        // AnEvenSplitOnOneNodeAlsoWithholds, asserting only that the offset is absent, has been doing.
        GridSceneMemory memory = new(richTextTie: true, secondRichTextLabel: true);
        DriverResult result = Calibrate(memory);

        // Withholding correctly and saying nothing about it are two different acts, and only the
        // first one had been implemented here. This path wrote a note and returned, so
        // `derivation.notDerived` stayed empty, and profile.agreement — which cannot read notes and
        // must not guess — scored the absence as an unexplained gap. Measured on 4.5-debug-gdscript:
        // richTextLabel.text tied 0xa80 (correct) against 0x1008 (memory noise that decoded), and the
        // cell failed for having withheld rather than for anything it got wrong.
        Assert.True(result.Derivation.NotDerived.ContainsKey(OffsetKeys.RichTextLabelText),
            "the even split withheld richTextLabel.text without declaring it");

        // And the reason has to be the REAL one. Declaring a plausible-sounding cause would leave the
        // tie unreported, which is the failure this whole change exists to surface — so the candidate
        // offsets that tied, and the node they tied on, must appear in it.
        string reason = result.Derivation.NotDerived[OffsetKeys.RichTextLabelText];
        Assert.Contains("split evenly", reason, StringComparison.Ordinal);
        Assert.Contains(Wire.Offset(GridSceneMemory.RichTextLabelText), reason, StringComparison.Ordinal);
        Assert.Matches(@"0x[0-9a-f]+=""", reason);
    }

    [Fact]
    public void AWithheldOffsetDeclaresTheObstacleThatStoppedIt()
    {
        GridSceneMemory memory = new(unreadableParentWindow: true);
        DriverResult result = Calibrate(memory);

        // The generic withhold — every key that goes through Publish and whose candidate set does not
        // resolve. canvasItem.visible took this path on 4.5-release-dotnet ("2 candidates survived
        // (0x370, 0x400)") and node.parent takes it here; the branch is the same one, and it declared
        // nothing for either.
        Assert.False(result.Derivation.Structural.Offsets.ContainsKey(OffsetKeys.NodeParent));
        Assert.True(result.Derivation.NotDerived.ContainsKey(OffsetKeys.NodeParent),
            "node.parent was withheld without declaring it");

        // The obstacle the candidate set actually presents, not a summary of it.
        string reason = result.Derivation.NotDerived[OffsetKeys.NodeParent];
        Assert.True(reason.Trim().Length >= 8, $"reason \"{reason}\" explains nothing");
        Assert.Contains("sample", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASingletonClassRefusalIsDeclaredAndIsNotOverwrittenByAVaguerOne()
    {
        GridSceneMemory memory = new(suppressRichText: true);
        DriverResult result = Calibrate(memory);

        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.RichTextLabelText));
        Assert.True(result.Derivation.NotDerived.ContainsKey(OffsetKeys.RichTextLabelText),
            "richTextLabel.text was withheld without declaring it");

        // Two refusals fire for this one key: OfClass rejects it because the class has a single
        // member, and the empty-pool branch a few lines later rejects the now-empty pool as "nothing
        // matched the layout". Both statements are true; only the first is the reason the key was
        // withheld. Declaring the second would be declaring a cause that is not the operative one —
        // the precise way T3 can be got wrong — so the first refusal is the one that stands.
        string reason = result.Derivation.NotDerived[OffsetKeys.RichTextLabelText];
        Assert.Contains("exactly ONE node of class", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("RichTextLabel layout", reason, StringComparison.Ordinal);

        // ...and the broader observation is not lost, it just is not the declaration.
        Assert.Contains(result.Notes, n => n.Contains("RichTextLabel layout", StringComparison.Ordinal));
    }

    [Fact]
    public void ATextClassWithNoSurvivingCandidateDeclaresItself()
    {
        GridSceneMemory memory = new(unsharedXlText: true);
        DriverResult result = Calibrate(memory);

        // label.text has two instances, so the singleton rule never fires here — the bracket simply
        // rejected every candidate. A refusal reached by applying a rule and finding nothing is still
        // a refusal, and is indistinguishable from never having looked unless it says so.
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.LabelText));
        Assert.True(result.Derivation.NotDerived.ContainsKey(OffsetKeys.LabelText),
            "label.text was withheld without declaring it");
        Assert.Contains("Label layout", result.Derivation.NotDerived[OffsetKeys.LabelText],
            StringComparison.Ordinal);
    }

    [Fact]
    public void NotOneTextLessNodeIsGivenAString()
    {
        // The assertion the harness itself was missing: it only ever inspected the three nodes that
        // DO have text, so inventing strings for the other seventeen scored identically.
        foreach (GridSceneMemory memory in new[]
        {
            new GridSceneMemory(),
            new GridSceneMemory(richTextTie: true),
            new GridSceneMemory(phantomTextOnPlainNodes: true),
            new GridSceneMemory(headerJunkString: true),
            new GridSceneMemory(headerJunkString: true, suppressRichText: true),
            new GridSceneMemory(controlLevelString: true),
        })
        {
            DriverResult result = Calibrate(memory);
            foreach (GridNode authored in GridScene.Nodes.Where(n => n.Text is null))
            {
                Assert.Null(result.Nodes.Single(n => n.Name == authored.Name).Text);
            }
        }
    }

    [Fact]
    public void AStringEveryControlCarriesIsNotALabelField()
    {
        GridSceneMemory memory = new(controlLevelString: true, secondRichTextLabel: true);
        DriverResult result = Calibrate(memory);

        // It is above the floor, it passes every CowData check, it satisfies the bracket, and it
        // covers more nodes than the real field — so grouping by "largest node set" hands it the
        // class outright. What disqualifies it is that a Label field cannot exist on every Control:
        // no validity check can supply that signal, because the string really is a valid one.
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", result.Nodes.Single(n => n.Name == "ZetaRich").Text);
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
    }

    [Fact]
    public void ANodeMemberCannotBeReportedAboveAControlMember()
    {
        GridSceneMemory memory = new(highScriptInstance: true);
        DriverResult result = Calibrate(memory);

        // The slot really does hold a pointer whose back-reference is the node, so pointer identity
        // is satisfied and cannot catch this. Single inheritance can: a base class is laid out before
        // the classes derived from it, so an Object member above control.size is impossible.
        Assert.False(result.Derivation.Structural.Offsets.ContainsKey(OffsetKeys.NodeScriptInstance));
        Assert.Contains(result.Notes, n => n.Contains("structurally impossible", StringComparison.Ordinal));

        // ...and withdrawing it leaves everything else standing.
        Assert.Equal(GridSceneMemory.NodeParent, Offset(result.Derivation.Structural.Offsets, OffsetKeys.NodeParent));
        Assert.Equal(GridSceneMemory.ControlSize, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.ControlSize));
    }

    [Fact]
    public void OneUnrelatedControlCannotVetoTheVisibleFlag()
    {
        GridSceneMemory memory = new(oneControlVetoes: true);
        DriverResult result = Calibrate(memory);

        // Measured once on a real cell: the true offset was nominated and then eliminated because a
        // single other Control did not satisfy the shape. The inheritance bracket that replaced the
        // universality rule — node.name < canvasItem.visible < control.offset — comes from offsets
        // the structural pass derived by pointer identity, and nothing in a live scene can veto it.
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
    }

    [Fact]
    public void TheVisibleBracketRejectsAnythingAtOrBelowTheNodeMembers()
    {
        GridSceneMemory memory = new();
        ulong visible = memory.ByPath["RootHarness/AlphaPanel/BetaBranch/VisibleTwin"];
        ulong hidden = memory.ByPath["RootHarness/AlphaPanel/BetaBranch/HiddenTwin"];

        NodeSample on = new(visible, MemoryWindow.Read(memory, visible, 0xC00));
        NodeSample off = new(hidden, MemoryWindow.Read(memory, hidden, 0xC00));
        List<string> diagnostics = [];

        // With the lower bound raised above the true offset, it must be rejected BY NAME rather than
        // silently — the diagnostics are what turned a whole matrix run into one line of explanation.
        OffsetCandidates result = new SemanticCalibrator(GodotPrecisionWidth.Single)
            .DeriveVisible(on, off, [on, off], GridSceneMemory.ControlOffsets, diagnostics, GridSceneMemory.ControlScale);

        Assert.False(result.TryGetOffset(out _));
        Assert.Contains(diagnostics, d => d.Contains("cannot be a CanvasItem field", StringComparison.Ordinal));
    }
}
