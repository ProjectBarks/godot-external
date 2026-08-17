using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;

namespace Godot.External.Tests;

/// <summary>
/// The claim §8.9 exists to test, made against a synthetic build: the calibrator solves the layout
/// from values alone.
/// </summary>
/// <remarks>
/// A synthetic target is not compatibility evidence and this file is not pretending otherwise — the
/// fixture writes the very offsets the calibrator then rediscovers, so a green run says the
/// technique works, not that any real Godot build is supported. What it does buy is the thing the
/// grid cannot give until Godot is installed: every decoy §12.5 warned about, present at once, with
/// a deterministic verdict.
/// </remarks>
public sealed class CalibrationSessionTests
{
    private static (DriverResult Result, GridSceneMemory Memory) Calibrate(bool duplicateLabelText = false)
    {
        GridSceneMemory memory = new(duplicateLabelText, secondRichTextLabel: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);
        return (result, memory);
    }

    private static int Offset(IReadOnlyDictionary<string, string> offsets, string key)
    {
        Assert.True(offsets.ContainsKey(key), $"{key} was not derived");
        return Convert.ToInt32(offsets[key][2..], 16);
    }

    [Fact]
    public void DerivesTheStructuralOffsetsByPointerIdentity()
    {
        (DriverResult result, _) = Calibrate();

        Assert.Equal(DerivationMethods.Structural, result.Derivation.Structural.Method);
        Assert.Equal(GridSceneMemory.NodeParent, Offset(result.Derivation.Structural.Offsets, OffsetKeys.NodeParent));
        Assert.Equal(GridSceneMemory.NodeChildListHead, Offset(result.Derivation.Structural.Offsets, OffsetKeys.NodeChildListHead));
        Assert.Equal(GridSceneMemory.NodeScriptInstance, Offset(result.Derivation.Structural.Offsets, OffsetKeys.NodeScriptInstance));

        Assert.Equal(GridSceneMemory.ChildLinkNext, Offset(result.Derivation.Walk.Offsets, OffsetKeys.ChildListNext));
        Assert.Equal(GridSceneMemory.ChildLinkPayload, Offset(result.Derivation.Walk.Offsets, OffsetKeys.ChildListNode));
        Assert.Equal(GridSceneMemory.ScriptInstanceOwner, Offset(result.Derivation.Walk.Offsets, OffsetKeys.ScriptInstanceOwner));
    }

    [Fact]
    public void DerivesTheSemanticOffsetsByKnownValueIntersection()
    {
        (DriverResult result, _) = Calibrate();
        IReadOnlyDictionary<string, string> offsets = result.Derivation.Semantic.Offsets;

        Assert.Equal(DerivationMethods.Semantic, result.Derivation.Semantic.Method);
        Assert.Equal(GridSceneMemory.ControlSize, Offset(offsets, OffsetKeys.ControlSize));
        Assert.Equal(GridSceneMemory.ControlOffsets, Offset(offsets, OffsetKeys.ControlOffset));
        Assert.Equal(GridSceneMemory.ControlPosition, Offset(offsets, OffsetKeys.ControlPosition));
        Assert.Equal(GridSceneMemory.ControlScale, Offset(offsets, OffsetKeys.ControlScale));
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(offsets, OffsetKeys.CanvasItemVisible));
    }

    [Fact]
    public void SizeSurvivesTheFourCandidateTrapOnlyByIntersecting()
    {
        (DriverResult result, _) = Calibrate();

        // §12.5: AlphaPanel alone offers 0x4c0, 0x4c8, 0x4d4 and 0x4f4. The reported candidate set is
        // what survived every sample, so exactly one may remain.
        Assert.Equal(new[] { "0x4c0" }, result.Derivation.Semantic.Candidates[OffsetKeys.ControlSize]);
        Assert.True(result.Derivation.Semantic.Samples[OffsetKeys.ControlSize] >= 2);
    }

    [Fact]
    public void ReadsRawOffsetsRatherThanAnchorsOrTheResolvedRect()
    {
        (DriverResult result, _) = Calibrate();
        NodeRecord anchored = result.Nodes.Single(n => n.Name == "AnchoredWide");

        Assert.Equal(new double[] { -431, -197, 182, -46 }, anchored.Offset);
        Assert.NotEqual(new double[] { 0.5, 0.5, 0.5, 0.5 }, anchored.Offset);
        Assert.Equal(new double[] { 12.5, -40.5 }, anchored.Position);
        Assert.Equal(new double[] { 613, 151 }, anchored.Size);
    }

    [Fact]
    public void ReproducesEveryAuthoredNode()
    {
        (DriverResult result, GridSceneMemory memory) = Calibrate();

        Assert.Equal(memory.Nodes.Count, result.WalkCount);
        Assert.Equal(memory.Nodes.Count, result.Nodes.Count);

        foreach (GridNode expected in memory.Nodes)
        {
            NodeRecord actual = result.Nodes.Single(n => n.Name == expected.Name);
            Assert.Equal(expected.Text, actual.Text);

            // A node with no CanvasItem base has no geometry and no visibility, and the authored
            // scene records null for both. The fixture still lays bytes down at those offsets —
            // that is what a real object does — so this is the assertion that the reader declines to
            // read them, rather than that they are absent from memory.
            if (expected.Class is "Node")
            {
                Assert.Null(actual.Size);
                Assert.Null(actual.Position);
                Assert.Null(actual.Scale);
                Assert.Null(actual.Offset);
                Assert.Null(actual.Visible);
                continue;
            }

            Assert.Equal(expected.Size, actual.Size);
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.Scale, actual.Scale);
            Assert.Equal(expected.Offsets, actual.Offset);
            Assert.Equal(expected.Visible, actual.Visible);
        }
    }

    [Fact]
    public void ChildListOrderAndParentPointersRoundTrip()
    {
        (DriverResult result, GridSceneMemory memory) = Calibrate();
        Dictionary<string, NodeRecord> byPointer = result.Nodes.ToDictionary(n => n.NativePtr);

        foreach (GridNode expected in memory.Nodes)
        {
            NodeRecord actual = byPointer["0x" + memory.ByPath[expected.Path].ToString("x")];
            string[] wanted = [.. memory.ChildrenOf(expected.Path).Select(c => "0x" + memory.ByPath[c.Path].ToString("x"))];
            Assert.Equal(wanted, actual.ChildPtrs);

            foreach (string child in actual.ChildPtrs)
            {
                Assert.Equal(actual.NativePtr, byPointer[child].ParentPtr);
            }
        }
    }

    [Fact]
    public void DuplicatedSizeDoesNotCollapseTwoNodes()
    {
        (DriverResult result, _) = Calibrate();

        NodeRecord leaf = result.Nodes.Single(n => n.Name == "AlphaLeaf");
        NodeRecord nest = result.Nodes.Single(n => n.Name == "GammaNest");

        Assert.Equal(new double[] { 409, 151 }, leaf.Size);
        Assert.Equal(new double[] { 409, 151 }, nest.Size);
        Assert.NotEqual(leaf.NativePtr, nest.NativePtr);
    }

    [Fact]
    public void DecodesUtf32TextIncludingAnAstralCodepoint()
    {
        (DriverResult result, _) = Calibrate();

        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
        Assert.Equal("héllo ✦ 日本語", result.Nodes.Single(n => n.Name == "ZetaLabelUnicode").Text);

        NodeRecord rich = result.Nodes.Single(n => n.Name == "ZetaRich");
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", rich.Text);
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
    }

    [Fact]
    public void TheTranslatedCopyIsExcludedByLayoutRatherThanByPreference()
    {
        (DriverResult result, _) = Calibrate();

        // A Label stores its text twice: `String text` then `String xl_text`, sharing one allocation
        // while nothing is translated, so both decode identically and no value can separate them.
        // The layout can: only `text` has the packed alignment enums in the qword behind it, while
        // `xl_text` has a pointer there. So this is not "prefer the lower of the pair" — the pair
        // never forms, and exactly one candidate survives.
        Assert.Equal("héllo ✦ 日本語", result.Nodes.Single(n => n.Name == "ZetaLabelUnicode").Text);
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
        Assert.Equal(new[] { "0x7f8" }, result.Derivation.Semantic.Candidates[OffsetKeys.LabelText]);
    }

    [Fact]
    public void NeverClaimsToHaveUsedAProfile()
    {
        (DriverResult result, _) = Calibrate();

        Assert.False(result.UsedProfile);
        Assert.DoesNotContain("profileConsulted", result.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void CrossChecksAgainstTheShippedTableOnlyAfterDeriving()
    {
        (DriverResult result, _) = Calibrate();

        Assert.NotNull(result.ProfileCrossCheck);
        Assert.Contains("agree", result.ProfileCrossCheck!["status"], StringComparison.Ordinal);
        Assert.Single(result.ProfileCrossCheck);
    }

    [Fact]
    public void FollowsTheManagedBridgeFromTheNativeSide()
    {
        GridSceneMemory memory = new();
        FakeManagedProbe probe = new(memory.ManagedObject, memory.Root);
        DriverResult result = new CalibrationSession(memory, GridScene.Request(), probe).Run(memory.Root);

        Assert.NotNull(result.ManagedBridge);
        Assert.Equal("Probe", result.ManagedBridge!.StaticRootType);
        Assert.Equal("0x" + memory.Root.ToString("x"), result.ManagedBridge.NativePtr);
        Assert.Equal("0x" + memory.Root.ToString("x"), result.ManagedBridge.Reverse!.OwnerBackref);
        Assert.Equal(GridSceneMemory.ScriptInstanceGcHandle, Offset(result.Derivation.Walk.Offsets, OffsetKeys.ScriptInstanceGcHandle));
        Assert.Equal("GridProbe ASCII 0123", result.ManagedBridge.Fields!["ProbeAscii"]);
    }

    [Fact]
    public void ReportsNothingWhenTheRootIsNotWhereItWasSaidToBe()
    {
        GridSceneMemory memory = new();
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root + 0x40);

        Assert.Equal(0, result.WalkCount);
        Assert.Empty(result.Derivation.Structural.Offsets);
        Assert.Empty(result.Derivation.Semantic.Offsets);
        Assert.Contains(result.Notes, n => n.Contains("no node layout reproduced", StringComparison.Ordinal));
    }

    /// <summary>
    /// A CLR that answers for exactly one address. Strictness is the test: every slot on the
    /// ScriptInstance gets dereferenced and offered, so a probe that said "yes" to anything would
    /// let the calibrator report the first slot it tried as the GCHandle.
    /// </summary>
    private sealed class FakeManagedProbe(ulong managedObject, ulong nativePtr) : IManagedProbe
    {
        public bool TryDescribe(ulong address, IReadOnlyList<string> fieldNames, out ManagedObjectInfo info)
        {
            info = new ManagedObjectInfo(string.Empty, 0, new Dictionary<string, object?>());
            if (address != managedObject)
            {
                return false;
            }

            info = new ManagedObjectInfo(
                "Probe",
                nativePtr,
                new Dictionary<string, object?> { ["ProbeAscii"] = "GridProbe ASCII 0123" });
            return true;
        }
    }
}
