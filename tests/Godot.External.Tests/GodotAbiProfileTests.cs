using Godot.External.Abi;

namespace Godot.External.Tests;

/// <summary>
/// Locks the recovered offset tables (docs/analysis.md §4.6) and the rules around them: the variant
/// flag is the export template, not the engine version (§4.6), and a shipped table is a fast path
/// that calibration may overwrite (§12.5).
/// </summary>
public class GodotAbiProfileTests
{
    [Theory]
    [InlineData(GodotField.CanvasItemVisible, 0x370)]
    [InlineData(GodotField.ControlGlobalPosition, 0x3f8)]
    [InlineData(GodotField.ControlOffsets, 0x470)]
    [InlineData(GodotField.ControlScale, 0x4a8)]
    [InlineData(GodotField.ControlPosition, 0x4b8)]
    [InlineData(GodotField.ControlSize, 0x4c0)]
    [InlineData(GodotField.NodeParent, 0x128)]
    [InlineData(GodotField.NodeChildListHead, 0x148)]
    [InlineData(GodotField.NodeName, 0x1c0)]
    [InlineData(GodotField.NodeScriptInstance, 0x68)]
    [InlineData(GodotField.LabelText, 0x7f8)]
    [InlineData(GodotField.RichTextLabelText, 0xa78)]
    public void ReleaseProfile_HasTheLiveValidatedOffsets(GodotField field, int expected)
    {
        Assert.Equal(expected, GodotAbiProfiles.Godot451Release.Offsets[field]);
    }

    [Theory]
    [InlineData(GodotField.CanvasItemVisible, 0x378)]
    [InlineData(GodotField.ControlGlobalPosition, 0x400)]
    [InlineData(GodotField.ControlOffsets, 0x478)]
    [InlineData(GodotField.ControlScale, 0x4b0)]
    [InlineData(GodotField.ControlPosition, 0x4c0)]
    [InlineData(GodotField.ControlSize, 0x4c8)]
    [InlineData(GodotField.NodeParent, 0x130)]
    [InlineData(GodotField.NodeChildListHead, 0x150)]
    [InlineData(GodotField.NodeName, 0x1c8)]
    [InlineData(GodotField.NodeScriptInstance, 0x70)]
    [InlineData(GodotField.LabelText, 0x800)]
    [InlineData(GodotField.RichTextLabelText, 0xa80)]
    public void DebugProfile_HasTheMeasuredOffsets(GodotField field, int expected)
    {
        Assert.Equal(expected, GodotAbiProfiles.Godot451Debug.Offsets[field]);
    }

    [Fact]
    public void MechanismConstants_AreSharedByBothTemplates()
    {
        // Only the field offsets differ between templates; the CowData/StringName/child-list
        // mechanisms are engine-wide (§4.6).
        foreach (GodotAbiProfile profile in GodotAbiProfiles.All)
        {
            Assert.Equal(0x00, profile.Offsets.ChildLinkNext);
            Assert.Equal(0x18, profile.Offsets.ChildLinkPayload);
            Assert.Equal(0x08, profile.Offsets.StringNameDataToBuffer);
            Assert.Equal(8, profile.Offsets.CowDataSizeBackOffset);
            Assert.Equal(0x08, profile.Offsets.ScriptInstanceOwner);
            Assert.Equal(0x20, profile.Offsets.ScriptInstanceGcHandle);
        }
    }

    [Fact]
    public void OnlyTheReleaseProfileClaimsValidation()
    {
        Assert.Equal(AbiConfidence.LiveValidated, GodotAbiProfiles.Godot451Release.Confidence);
        Assert.Equal(AbiConfidence.Calibrated, GodotAbiProfiles.Godot451Debug.Confidence);
    }

    [Fact]
    public void ReleaseControlOffsets_AreAscendingAndNonOverlapping()
    {
        // §4.6's independent corroboration: the release column reproduces upstream Control::Data
        // ordering — offset[4] -> anchor[4] -> ... -> scale -> pos_cache -> size_cache.
        GodotAbiProfile profile = GodotAbiProfiles.Godot451Release;
        GodotOffsetTable offsets = profile.Offsets;

        Assert.True(offsets.ControlOffsets + (4 * profile.RealSize) <= offsets.ControlScale);
        Assert.True(offsets.ControlScale + (2 * profile.RealSize) <= offsets.ControlPosition);
        Assert.True(offsets.ControlPosition + (2 * profile.RealSize) <= offsets.ControlSize);
    }

    [Fact]
    public void DebugColumn_IsReleasePlusEightUniformly()
    {
        // Was a regression guard on a KNOWN DEFECT: §4.6's debug column had getOffset spanning
        // 0x500..0x50c while getPosition read 0x508, which cannot both be true, and the test asserted
        // the overlap so nobody would "fix" the table by guessing. The ABI grid has since derived the
        // column from stock debug templates — three passes, unchanged binaries, no contradictions —
        // and the real relationship is a uniform +8. The guard now protects the measurement instead
        // of the defect.
        GodotOffsetTable release = GodotAbiProfiles.Godot451Release.Offsets;
        GodotOffsetTable debug = GodotAbiProfiles.Godot451Debug.Offsets;

        foreach (GodotField field in new[]
        {
            GodotField.CanvasItemVisible, GodotField.ControlGlobalPosition, GodotField.ControlOffsets,
            GodotField.ControlScale, GodotField.ControlPosition, GodotField.ControlSize,
            GodotField.NodeParent, GodotField.NodeChildListHead, GodotField.NodeName,
            GodotField.NodeScriptInstance, GodotField.LabelText, GodotField.RichTextLabelText,
        })
        {
            Assert.Equal(release[field] + 8, debug[field]);
        }
    }

    [Fact]
    public void DebugControlOffsets_AreAscendingAndNonOverlapping()
    {
        GodotAbiProfile profile = GodotAbiProfiles.Godot451Debug;
        GodotOffsetTable offsets = profile.Offsets;

        Assert.True(offsets.ControlOffsets + (4 * profile.RealSize) <= offsets.ControlScale);
        Assert.True(offsets.ControlScale + (2 * profile.RealSize) <= offsets.ControlPosition);
        Assert.True(offsets.ControlPosition + (2 * profile.RealSize) <= offsets.ControlSize);
    }

    [Theory]
    [InlineData("4.5.1", "4.5.1", GodotBuildTemplate.Release)]
    [InlineData("4.5.1-debug", "4.5.1", GodotBuildTemplate.Debug)]
    [InlineData("  4.3-debug  ", "4.3", GodotBuildTemplate.Debug)]
    [InlineData("9.0.2", "9.0.2", GodotBuildTemplate.Release)]
    [InlineData("4.5.1-rc1", "4.5.1-rc1", GodotBuildTemplate.Release)]
    public void TemplateComesFromTheDebugSuffix_NotTheVersion(string input, string version, GodotBuildTemplate template)
    {
        (string parsedVersion, GodotBuildTemplate parsedTemplate) = GodotAbiProfiles.ParseVersionString(input);

        Assert.Equal(version, parsedVersion);
        Assert.Equal(template, parsedTemplate);
    }

    [Fact]
    public void Lookup_SelectsTheTemplateImpliedByTheVersionString()
    {
        Assert.True(GodotAbiProfiles.TryGet("4.5.1", GodotPrecision.Single, out GodotAbiProfile? release));
        Assert.NotNull(release);
        Assert.Equal(GodotBuildTemplate.Release, release.Template);

        Assert.True(GodotAbiProfiles.TryGet("4.5.1-debug", GodotPrecision.Single, out GodotAbiProfile? debug));
        Assert.NotNull(debug);
        Assert.Equal(GodotBuildTemplate.Debug, debug.Template);
    }

    [Fact]
    public void UnmeasuredCells_AreNotGuessed()
    {
        // Double precision and other engine versions have never been measured; §8.9 says say so
        // rather than returning a neighbouring cell.
        Assert.False(GodotAbiProfiles.TryGet("4.5.1", GodotPrecision.Double, out _));
        Assert.False(GodotAbiProfiles.TryGet("4.3", GodotPrecision.Single, out _));
    }

    [Fact]
    public void ComponentAddress_UsesRealSizeAsStride()
    {
        GodotAbiProfile profile = GodotAbiProfiles.Godot451Release;
        const ulong Control = 0x70000;

        // globalPosition x/y == 0x3f8/0x3fc, offset[0..3] == 0x470 + i*4 (§4.6).
        Assert.Equal(Control + 0x3f8, profile.ComponentAddress(Control, GodotField.ControlGlobalPosition, 0));
        Assert.Equal(Control + 0x3fc, profile.ComponentAddress(Control, GodotField.ControlGlobalPosition, 1));
        Assert.Equal(Control + 0x47c, profile.ComponentAddress(Control, GodotField.ControlOffsets, 3));
        Assert.Equal(Control + 0x4c4, profile.ComponentAddress(Control, GodotField.ControlSize, 1));
    }

    [Fact]
    public void DoublePrecision_WidensTheStride()
    {
        GodotAbiProfile doubled = GodotAbiProfiles.Godot451Release with { Precision = GodotPrecision.Double };

        Assert.Equal(8, doubled.RealSize);
        Assert.Equal(0x400ul, doubled.ComponentAddress(0, GodotField.ControlGlobalPosition, 1));
    }

    [Fact]
    public void CalibrationOverridesTheShippedTable_WithoutMutatingIt()
    {
        GodotAbiProfile shipped = GodotAbiProfiles.Godot451Release;

        GodotAbiProfile calibrated = shipped.WithCalibratedOffset(GodotField.NodeChildListHead, 0x158);

        Assert.Equal(0x158, calibrated.Offsets.NodeChildListHead);
        Assert.Equal(AbiConfidence.Calibrated, calibrated.Confidence);

        // §12.5: the table is a fast path and a cross-check, so the original must survive intact
        // for the divergence diff to mean anything.
        Assert.Equal(0x148, GodotAbiProfiles.Godot451Release.Offsets.NodeChildListHead);
        Assert.Equal(AbiConfidence.LiveValidated, GodotAbiProfiles.Godot451Release.Confidence);
    }

    [Fact]
    public void CalibratedCannotOutrankLiveValidated()
    {
        // The trust order is what consumers gate on, so it must not be possible for a partly
        // calibrated table to rank above one that was checked end to end.
        Assert.True(AbiConfidence.Unvalidated < AbiConfidence.Calibrated);
        Assert.True(AbiConfidence.Calibrated < AbiConfidence.LiveValidated);
    }

    [Fact]
    public void CalibratingOneFieldOfAnUnvalidatedTable_DoesNotLaunderIt()
    {
        // The failure this guards: one derived offset stamping a whole unproven column as
        // top-confidence, so `Confidence >= LiveValidated` accepts seventeen numbers nobody measured.
        //
        // It used to use the shipped debug profile as its unvalidated base. That profile is now
        // grid-measured and claims Calibrated, so the base is built here instead — the RULE is what
        // this test is about, and it must not quietly stop testing it because a table improved.
        GodotAbiProfile unproven = GodotAbiProfiles.Godot451Debug with
        {
            Confidence = AbiConfidence.Unvalidated,
            Notes = "hypothetical unvalidated column",
        };

        GodotAbiProfile calibrated = unproven.WithCalibratedOffset(GodotField.ControlSize, 0x4c0);

        Assert.Equal(AbiConfidence.Unvalidated, calibrated.Confidence);
        Assert.True(calibrated.Confidence < AbiConfidence.LiveValidated);
        Assert.True(calibrated.IsCalibrated(GodotField.ControlSize));
        Assert.False(calibrated.IsCalibrated(GodotField.ControlPosition));
    }

    [Fact]
    public void CalibratingOneFieldOfTheValidatedTable_DemotesIt()
    {
        GodotAbiProfile calibrated = GodotAbiProfiles.Godot451Release
            .WithCalibratedOffset(GodotField.NodeParent, 0x130);

        Assert.Equal(AbiConfidence.Calibrated, calibrated.Confidence);
        Assert.True(calibrated.Confidence < AbiConfidence.LiveValidated);
    }

    [Fact]
    public void WhollyCalibratedTable_ClaimsCalibrated_AndMarksEveryField()
    {
        GodotOffsetTable derived = GodotAbiProfiles.Godot451Release.Offsets with { NodeParent = 0x130 };

        GodotAbiProfile fromDebugBase = GodotAbiProfiles.Godot451Debug.WithCalibratedOffsets(derived);
        GodotAbiProfile fromReleaseBase = GodotAbiProfiles.Godot451Release.WithCalibratedOffsets(derived);

        // None of the base table's numbers survive, so the result is neither promoted by nor
        // dragged down by where it started.
        Assert.Equal(AbiConfidence.Calibrated, fromDebugBase.Confidence);
        Assert.Equal(AbiConfidence.Calibrated, fromReleaseBase.Confidence);

        foreach (GodotField field in Enum.GetValues<GodotField>())
        {
            Assert.True(fromDebugBase.IsCalibrated(field));
        }
    }

    [Fact]
    public void ShippedProfiles_ClaimNoCalibratedFields()
    {
        foreach (GodotAbiProfile profile in GodotAbiProfiles.All)
        {
            Assert.Equal(0ul, profile.CalibratedFieldMask);

            foreach (GodotField field in Enum.GetValues<GodotField>())
            {
                Assert.False(profile.IsCalibrated(field));
            }
        }
    }

    [Fact]
    public void ProfilesWithIdenticalDataCompareEqual()
    {
        // Per-field provenance is a value-typed mask precisely so record equality keeps working.
        GodotAbiProfile left = GodotAbiProfiles.Godot451Release.WithCalibratedOffset(GodotField.NodeParent, 0x130);
        GodotAbiProfile right = GodotAbiProfiles.Godot451Release.WithCalibratedOffset(GodotField.NodeParent, 0x130);

        Assert.Equal(left, right);
        Assert.NotEqual(left, GodotAbiProfiles.Godot451Release);
    }

    [Fact]
    public void EveryFieldIsReadableAndWritableByEnum()
    {
        // The calibrator addresses offsets as data, so no field may be missing from Get/With.
        GodotOffsetTable offsets = GodotAbiProfiles.Godot451Release.Offsets;

        foreach (GodotField field in Enum.GetValues<GodotField>())
        {
            int original = offsets[field];
            GodotOffsetTable updated = offsets.With(field, original + 0x10);

            Assert.Equal(original + 0x10, updated[field]);
            Assert.Equal(original, offsets[field]);
        }
    }

    [Fact]
    public void UnknownField_Throws()
    {
        GodotOffsetTable offsets = GodotAbiProfiles.Godot451Release.Offsets;

        Assert.Throws<ArgumentOutOfRangeException>(() => offsets[(GodotField)9999]);
        Assert.Throws<ArgumentOutOfRangeException>(() => offsets.With((GodotField)9999, 0));
    }
}
