using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;
using Godot.External.Calibrator.Target;

namespace Godot.External.Tests;

/// <summary>
/// Regressions for the four defects the first real grid run exposed.
/// </summary>
/// <remarks>
/// Every fixture flag used here reproduces something that was <em>measured</em> on a live export,
/// not something imagined: a structure reaching a name buffer at a distance other than
/// <c>StringName::_Data</c>'s, a one-off string on a Label that is not its text, a child listed
/// twice by a candidate layout, and a second back-reference on a scene where only the root is
/// scripted.
/// </remarks>
public sealed class CalibrationDefectTests
{
    private static int Offset(IReadOnlyDictionary<string, string> offsets, string key)
    {
        Assert.True(offsets.ContainsKey(key), $"{key} was not derived");
        return Convert.ToInt32(offsets[key][2..], 16);
    }

    // -- A: root location must not depend on hash ordering --------------------

    [Fact]
    public void LocatesTheRootWhenAnotherStructureReachesTheNameAtADifferentDistance()
    {
        GridSceneMemory memory = new(decoyNameData: true);
        RegionScanner scanner = new(memory, memory);

        // Guard the fixture itself: this test is only meaningful while two different structures
        // really do reach the root's character buffer, so assert that before asserting the fix.
        IReadOnlyList<ulong> buffers = scanner.FindBytes(GodotText.Utf32Needle("RootHarness"), 64);
        Assert.Single(buffers);
        Assert.True(
            scanner.FindPointersTo(buffers, 64)[buffers[0]].Count >= 2,
            "fixture no longer reproduces the two-distance condition this test exists for");

        RootLocator locator = new(memory, scanner);
        List<string> notes = [];

        RootLocation? located = locator.Locate(
            "RootHarness",
            ["AlphaPanel", "OmegaPanel"],
            [.. GridScene.Nodes.Select(n => n.Name)],
            GridScene.Nodes.Count,
            notes);

        // The decoy validates at k=24 and the real StringName at k=8. Carrying one distance per NAME
        // let the decoy's win and then rejected every child for disagreeing with it; carrying one per
        // SLOT pairs the real slots with each other regardless of which was seen first.
        Assert.NotNull(located);
        Assert.Equal(memory.Root, located!.Root);
        Assert.Equal(8, located.StringNameDataToBuffer);
        Assert.Equal(GridSceneMemory.NodeName, located.Anchor.NameOffset);
        Assert.Equal(GridSceneMemory.NodeParent, located.Anchor.ParentOffset);
    }

    [Fact]
    public void RootLocationIsRepeatableOnAnUnchangingTarget()
    {
        GridSceneMemory memory = new(decoyNameData: true);

        // The original defect showed up as roughly one run in five succeeding against a target that
        // had not changed at all, so repeating the search is the assertion that matters.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            List<string> notes = [];
            RootLocation? located = new RootLocator(memory, new RegionScanner(memory, memory)).Locate(
                "RootHarness",
                ["AlphaPanel", "OmegaPanel"],
                [.. GridScene.Nodes.Select(n => n.Name)],
                GridScene.Nodes.Count,
                notes);

            Assert.NotNull(located);
            Assert.Equal(memory.Root, located!.Root);
        }
    }

    // -- B: an ambiguous offset must not withhold an unambiguous string -------

    [Fact]
    public void AStrayStringOnALabelIsNotMistakenForTheRichTextClass()
    {
        GridSceneMemory memory = new(duplicateLabelText: true, strayNodeString: true, secondRichTextLabel: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // The Labels carry text, a translated copy, and one unrelated one-off string. That stray is
        // the sole member of its own single-node group, so without the classes being disjoint it
        // wins richTextLabel.text outright — publishing a scene path as the RichTextLabel's text
        // offset and withholding the real one's string.
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
        Assert.Equal("héllo ✦ 日本語", result.Nodes.Single(n => n.Name == "ZetaLabelUnicode").Text);
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", result.Nodes.Single(n => n.Name == "ZetaRich").Text);

        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));
    }

    [Fact]
    public void AConstantSharedByBothLabelsDoesNotSuppressTheirText()
    {
        GridSceneMemory memory = new(sharedLabelConstant: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // The defect that cost strings.text.ascii and .unicode on every cell of a full matrix run.
        // A font path sits at a fixed offset on exactly the two Label nodes — the same node set as
        // `text` — so it entered the Label candidate pool and the pool could never agree on any node.
        // It is identical on both nodes and authored text is not, which is what tells them apart.
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
        Assert.Equal("héllo ✦ 日本語", result.Nodes.Single(n => n.Name == "ZetaLabelUnicode").Text);
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
    }

    // -- 1: a node that cannot answer must not veto the derivation ------------

    [Fact]
    public void AnUnreadableNodeWindowDoesNotWithholdTheVisibleFlag()
    {
        GridSceneMemory memory = new();
        NodeSample visible = Sample(memory, "RootHarness/AlphaPanel/BetaBranch/VisibleTwin");
        NodeSample hidden = Sample(memory, "RootHarness/AlphaPanel/BetaBranch/HiddenTwin");

        List<NodeSample> all = [.. GridScene.Nodes.Select(n => Sample(memory, n.Path))];

        // A plain Node is a far smaller allocation than a Control, so a Control-sized window off one
        // cannot be read in full. That is a fact about the heap, and it removed no candidate — but
        // requiring every node's window to be complete let it withhold the answer entirely. Measured
        // on the real grid: absent on 11 of 24 cell-runs, deterministically absent on two cells, and
        // never once wrong when present.
        all.Add(new NodeSample(0xDEAD_0000, MemoryWindow.Unreadable(0xDEAD_0000, 0xC00)));

        OffsetCandidates result = new SemanticCalibrator(GodotPrecisionWidth.Single)
            .DeriveVisible(visible, hidden, all, GridSceneMemory.ControlOffsets);

        Assert.True(result.TryGetOffset(out int offset), result.Obstacle());
        Assert.Equal(GridSceneMemory.CanvasItemVisible, offset);
    }

    private static NodeSample Sample(GridSceneMemory memory, string path)
    {
        ulong address = memory.ByPath[path];
        return new NodeSample(address, MemoryWindow.Read(memory, address, 0xC00));
    }

    // -- 2: a garbage read on one node must never take down the cell ----------

    [Fact]
    public void ANonControlNodeReportsNoGeometryRatherThanNonsense()
    {
        GridSceneMemory memory = new(nonControlSibling: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        NodeRecord plain = result.Nodes.Single(n => n.Name == "DeltaSiblingOne");

        // §12.4c: Control accessors on a non-Control succeed and return denormals — but they can
        // just as easily return all zeros, which is plausible geometry. What settles it is that the
        // engine says this object has no CanvasItem base at all.
        Assert.Equal("Node", plain.NodeClass);
        Assert.Null(plain.Size);
        Assert.Null(plain.Position);
        Assert.Null(plain.Scale);
        Assert.Null(plain.Offset);
        Assert.Null(plain.Visible);

        // ...and the other nineteen are unaffected, including the flag itself still being derived.
        Assert.Equal(new double[] { 613, 227 }, result.Nodes.Single(n => n.Name == "AlphaPanel").Size);
        Assert.True(result.Nodes.Single(n => n.Name == "AlphaPanel").Visible);
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
    }

    [Fact]
    public void AnInfiniteReadingSerialisesInsteadOfLosingTheWholeCell()
    {
        GridSceneMemory memory = new(nonControlSibling: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // Sanitised on the way in, and the serializer is configured to tolerate the literals anyway:
        // one Infinity used to throw out of ToJson and report the entire cell as `error`.
        string json = result.ToJson();
        Assert.DoesNotContain("Infinity", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN", json, StringComparison.Ordinal);
        Assert.Equal(GridScene.Nodes.Count, System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("nodes").GetArrayLength());
    }

    // -- 3: a second reading must actually be a second reading ----------------

    [Fact]
    public void PerFrameStateIsSeparatedFromTheFieldsItImitates()
    {
        GridSceneMemory memory = new(transients: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request(), null, memory.AdvanceFrame).Run(memory.Root);

        // Each of these has a single-reading impostor that is indistinguishable from the real field:
        // a boolean that is 1 on the visible twin and 0 on the hidden one, a shaped-text cache that
        // varies per Label, and an allocation that points back at the root. None survives a frame.
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
        Assert.Equal(GridSceneMemory.NodeScriptInstance, Offset(result.Derivation.Structural.Offsets, OffsetKeys.NodeScriptInstance));
        Assert.Equal(GridSceneMemory.ScriptInstanceOwner, Offset(result.Derivation.Walk.Offsets, OffsetKeys.ScriptInstanceOwner));
    }

    [Fact]
    public void TheStructuralDerivationsNeedNoSecondReadingAtAll()
    {
        GridSceneMemory memory = new(transients: true);

        // No refresh — exactly what a page cache does to a repeat read, so every temporal test is a
        // no-op. `visible` and the text offsets are unaffected, because they are settled by layout:
        // CanvasItem's Window* and boolean block, and Label's shared xl_text. This matters beyond
        // caching, because in a live UI `visible` genuinely toggles as panels animate — a
        // must-not-change test rejects the correct answer and leaves nothing.
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);

        // The ScriptInstance chain is the one that still needs it: distinguishing a real
        // ScriptInstance from a short-lived allocation that points back at the node is a question
        // about lifetime, and there is no layout fact that answers it.
        Assert.False(result.Derivation.Structural.Offsets.ContainsKey(OffsetKeys.NodeScriptInstance));
    }

    // -- C: a repeated child must not exit the process ------------------------

    [Fact]
    public void DuplicateChildAddressesDoNotCrashTheParentDerivation()
    {
        GridSceneMemory memory = new();
        ulong root = memory.Root;
        ulong child = memory.ByPath["RootHarness/AlphaPanel"];

        // A wrong link offset walks a chain that revisits a node, so a candidate layout really can
        // list the same child twice. FindPointerOffsetAcross throws on duplicate sample addresses,
        // and an exception here would leave the process via a non-zero exit — which this driver
        // reserves for "no pid", i.e. it would report a calibration condition as a crashed driver.
        OffsetCandidates candidates = new StructuralCalibrator(memory)
            .DeriveParent([(child, root), (child, root), (child, root)], 0xC00);

        Assert.True(candidates.TryGetOffset(out int offset));
        Assert.Equal(GridSceneMemory.NodeParent, offset);
        Assert.Equal(1, candidates.SampleCount);
    }

    [Fact]
    public void NoPairsIsAVerdictRatherThanAThrow()
    {
        GridSceneMemory memory = new();

        OffsetCandidates candidates = new StructuralCalibrator(memory).DeriveParent([], 0xC00);

        Assert.True(candidates.IsEmpty);
        Assert.False(candidates.IsDetermined);
    }

    // -- D: a second back-reference on a single-scripted scene ----------------

    [Fact]
    public void WithholdsScriptInstanceWhenPointerIdentityCannotSeparateTheCandidates()
    {
        GridSceneMemory memory = new(decoyScriptInstance: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        Assert.False(result.Derivation.Structural.Offsets.ContainsKey(OffsetKeys.NodeScriptInstance));
        Assert.Contains(result.Notes, n => n.Contains("Deferred to the managed bridge", StringComparison.Ordinal));
    }

    [Fact]
    public void TheManagedBridgeSettlesTheScriptInstanceItCannotDeriveAlone()
    {
        GridSceneMemory memory = new(decoyScriptInstance: true);
        FakeProbe probe = new(memory.ManagedObject, memory.Root);
        DriverResult result = new CalibrationSession(memory, GridScene.Request(), probe).Run(memory.Root);

        // Only the real ScriptInstance's GCHandle dereferences to a managed object of the expected
        // type, so following the candidates is a derivation and not a guess.
        Assert.Equal("0x68", result.Derivation.Structural.Offsets[OffsetKeys.NodeScriptInstance]);
        Assert.Equal("0x8", result.Derivation.Walk.Offsets[OffsetKeys.ScriptInstanceOwner]);
        Assert.Equal("0x20", result.Derivation.Walk.Offsets[OffsetKeys.ScriptInstanceGcHandle]);
        Assert.NotNull(result.ManagedBridge);
        Assert.Equal("0x" + memory.Root.ToString("x"), result.ManagedBridge!.NativePtr);
    }

    // -- D: the two-layout choice is deliberate -------------------------------

    [Fact]
    public void NamesTheForwardListWhenTwoLayoutsReproduceTheScene()
    {
        GridSceneMemory memory = new();
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // This fixture is singly linked, so only one layout survives here; the assertion that
        // travels is that whichever is chosen is the lowest head offset, which upstream declares as
        // List::first and whose chain is the authored child order.
        Assert.Equal(GridSceneMemory.NodeChildListHead, Convert.ToInt32(result.Derivation.Structural.Offsets[OffsetKeys.NodeChildListHead][2..], 16));

        string[] authored = [.. memory.ChildrenOf("RootHarness").Select(c => "0x" + memory.ByPath[c.Path].ToString("x"))];
        Assert.Equal(authored, result.Nodes.Single(n => n.Name == "RootHarness").ChildPtrs);
    }

    private sealed class FakeProbe(ulong managedObject, ulong nativePtr) : IManagedProbe
    {
        public bool TryDescribe(ulong address, IReadOnlyList<string> fieldNames, out ManagedObjectInfo info)
        {
            info = new ManagedObjectInfo(string.Empty, 0, new Dictionary<string, object?>());
            if (address != managedObject)
            {
                return false;
            }

            info = new ManagedObjectInfo("Probe", nativePtr, new Dictionary<string, object?>());
            return true;
        }
    }
}
