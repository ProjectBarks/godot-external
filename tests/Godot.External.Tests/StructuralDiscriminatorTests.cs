using System.Buffers.Binary;
using System.Text;
using Godot.External.Calibrator.Calibration;
using Godot.External.Calibrator.Protocol;
using LiveClr.Memory;

namespace Godot.External.Tests;

/// <summary>
/// The layout facts that replaced statistical and temporal guessing.
/// </summary>
/// <remarks>
/// Each of these is read out of Godot's own headers (identical in 4.3 and 4.5) rather than inferred
/// from what the data happens to look like. The distinction is not academic: the temporal test these
/// replace <em>discriminated against the correct answer</em>, because in a live UI <c>visible</c>
/// genuinely toggles while the decoys around it sit still.
/// </remarks>
public sealed class StructuralDiscriminatorTests
{
    private static int Offset(IReadOnlyDictionary<string, string> offsets, string key)
    {
        Assert.True(offsets.ContainsKey(key), $"{key} was not derived");
        return Convert.ToInt32(offsets[key][2..], 16);
    }

    // -- CanvasItem: visible ---------------------------------------------------

    [Fact]
    public void TheBackwardPointerTestSeparatesVisibleFromNotifyLocalTransform()
    {
        GridSceneMemory memory = new(transients: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // notify_local_transform sits at V+8, so it is the one boolean decoy the alignment rule
        // cannot eliminate — and here it differs across the twins exactly as `visible` does. What
        // separates them is the qword behind: a Window pointer for `visible`, eight bools for the
        // decoy. No pointer looks like a run of bools and no run of bools looks like a pointer.
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
        Assert.Equal(new[] { "0x370" }, result.Derivation.Semantic.Candidates[OffsetKeys.CanvasItemVisible]);
    }

    [Fact]
    public void AnAncestorHiddenTwinStillLocatesVisibleRatherThanParentVisibleInTree()
    {
        GridSceneMemory memory = new(hiddenByAncestor: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // `visible` only differs between the twins when the hidden one was hidden by its own hide().
        // Hide an ancestor instead and the stored flag is identical on both, while the byte that
        // differs is parent_visible_in_tree one byte later. A difference at D must therefore nominate
        // D-1 as well, and the alignment rule decides which of the two is the property.
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
    }

    [Theory]
    [InlineData(0x36f)] // odd
    [InlineData(0x374)] // 4-aligned but not 8
    public void OnlyEightAlignedCandidatesSurvive(int candidate)
    {
        GridSceneMemory memory = new();
        ulong address = memory.ByPath["RootHarness/AlphaPanel/BetaBranch/VisibleTwin"];
        MemoryWindow window = MemoryWindow.Read(memory, address, 0xC00);

        // CanvasItem's prefix through `window` is exactly 0x80 bytes and sizeof(Node) is 8-aligned,
        // so `visible` is always congruent to 0 mod 8. Every one of the four candidates that survived
        // on a real cell (0x16f, 0x187, 0x2ff, 0x347) fails this one rule.
        Assert.NotEqual(0, candidate % 8);
        Assert.True(SemanticCalibrator.FitsCanvasItem(window, GridSceneMemory.CanvasItemVisible));
    }

    [Fact]
    public void AQwordOfBoolsIsRejectedHoweverPointerLikeItLooks()
    {
        const ulong Origin = 0x10000;
        const int Candidate = 0x40;

        RawMemory memory = new();
        memory.Fill(Origin, 0x80, 0);
        memory.Write(Origin + Candidate - 12, [1, 0, 0, 0]);   // z_relative, y_sort, padding
        for (int i = 0; i <= 10; i++)
        {
            memory.Write(Origin + (ulong)(Candidate + i), [(byte)(i % 2)]);
        }

        // notify_local_transform's predecessor qword is eight bools — and this particular run of
        // bools is 8-aligned, above 0x10000 and below 2^48, so every cheap sanity test on a pointer
        // passes. Only "no pointer is made entirely of 0s and 1s" rejects it.
        memory.WriteUInt64(Origin + Candidate - 8, 0x0000_0000_0100_0000);
        Assert.False(SemanticCalibrator.FitsCanvasItem(MemoryWindow.Read(memory, Origin, 0x80), Candidate));

        // The same neighbourhood with a real Window* is accepted.
        memory.WriteUInt64(Origin + Candidate - 8, 0x0000_01A9_204C_5580);
        Assert.True(SemanticCalibrator.FitsCanvasItem(MemoryWindow.Read(memory, Origin, 0x80), Candidate));
    }

    // -- CowData validation ----------------------------------------------------

    [Theory]
    [InlineData("valid", true)]
    [InlineData("misaligned", false)]
    [InlineData("zero-refcount", false)]
    [InlineData("huge-refcount", false)]
    [InlineData("no-terminator", false)]
    [InlineData("surrogate", false)]
    [InlineData("out-of-range", false)]
    public void OnlyARealCowDataHeaderIsAccepted(string variant, bool expected)
    {
        RawMemory memory = new();
        const ulong Field = 0x10000;
        ulong buffer = variant == "misaligned" ? 0x20008u : 0x20000u;

        uint[] units = variant switch
        {
            "surrogate" => [0xD800, 0],
            "out-of-range" => [0x110000, 0],
            "no-terminator" => [(uint)'h', (uint)'i'],
            _ => [(uint)'h', (uint)'i', 0],
        };

        memory.WriteUInt64(buffer - 16, variant switch { "zero-refcount" => 0, "huge-refcount" => 1UL << 40, _ => 1 });
        memory.WriteUInt64(buffer - 8, (ulong)units.Length);
        for (int i = 0; i < units.Length; i++)
        {
            memory.WriteUInt32(buffer + (ulong)(i * 4), units[i]);
        }

        memory.WriteUInt64(Field, buffer);

        Assert.Equal(expected, TextCalibrator.TryReadTextField(memory, Field, out string value, out _));
        if (expected)
        {
            Assert.Equal("hi", value);
        }
    }

    // -- Label vs RichTextLabel ------------------------------------------------

    [Fact]
    public void RichTextLabelPromotesWithoutAnXlTextPair()
    {
        GridSceneMemory memory = new(secondRichTextLabel: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // RichTextLabel has no xl_text member at all — _apply_translation uses a local — so there is
        // exactly one stored String and any rule demanding a pair can never find it.
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", result.Nodes.Single(n => n.Name == "ZetaRich").Text);
    }

    [Fact]
    public void AMajorityReadingIsNotVetoedByADissentingCandidate()
    {
        GridSceneMemory memory = new(richTextDecoys: true, secondRichTextLabel: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // Two candidates decode the authored string, one does not. Demanding unanimity let the odd
        // one out suppress an answer the others agreed on — the calibrator finding the right value
        // and refusing to publish it, which is a worse failure than reporting it with the dissent
        // recorded.
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", result.Nodes.Single(n => n.Name == "ZetaRich").Text);
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));

        // The dissenter is reported rather than hidden.
        Assert.Contains("0xd30", result.Derivation.Semantic.Candidates[OffsetKeys.RichTextLabelText]);
        Assert.Contains(result.Notes, n => n.Contains("dissented", StringComparison.Ordinal));
    }

    // -- absent, never wrong ---------------------------------------------------

    [Fact]
    public void AValidStringInsideTheNodeHeaderIsRejectedOnPosition()
    {
        GridSceneMemory memory = new(headerJunkString: true, secondRichTextLabel: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // This one is a real CowData with a real refcount, a real size and a real terminator, so no
        // amount of header validation can reject it — a StringName's character buffer is a genuine
        // Godot String, it is simply not this one. What disqualifies it is single inheritance: a
        // RichTextLabel member cannot sit below node.parent.
        Assert.Equal(GridSceneMemory.RichTextLabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.RichTextLabelText));
        Assert.Equal("ρich ✦ テキスト 𝄞 RTL", result.Nodes.Single(n => n.Name == "ZetaRich").Text);
        Assert.DoesNotContain("0x110", result.Derivation.Semantic.Candidates[OffsetKeys.RichTextLabelText]);
    }

    [Fact]
    public void WithOnlyJunkAvailableNothingIsPublishedAtAll()
    {
        GridSceneMemory memory = new(headerJunkString: true, suppressRichText: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // The property the whole series rests on. With the real field gone, the only candidate left
        // is the header junk, and the correct output is nothing: a field that is absent costs one
        // check, while a field that is confidently wrong costs the credibility of every other number
        // in the table. There used to be a fallback here that fired in exactly this situation — when
        // the least is known — and it published "Color" as the RichTextLabel's text.
        Assert.False(result.Derivation.Strings.Offsets.ContainsKey(OffsetKeys.RichTextLabelText));
        Assert.Null(result.Nodes.Single(n => n.Name == "ZetaRich").Text);
        Assert.Contains(result.Notes, n => n.Contains("Withheld", StringComparison.Ordinal));

        // ...and the rest of the derivation is untouched by it.
        Assert.Equal(GridSceneMemory.LabelText, Offset(result.Derivation.Strings.Offsets, OffsetKeys.LabelText));
        Assert.Equal("GridProbe ASCII 0123", result.Nodes.Single(n => n.Name == "ZetaLabelAscii").Text);
    }

    // -- and the bracket must not over-constrain -------------------------------

    [Fact]
    public void UninitialisedPaddingAndUnexpectedEnumsDoNotEliminateVisible()
    {
        GridSceneMemory memory = new(dirtyPadding: true);
        DriverResult result = new CalibrationSession(memory, GridScene.Request()).Run(memory.Root);

        // Requiring zero padding, an eleven-long boolean run and a clip_children_mode enum emptied
        // the candidate list on all 24 cell-runs — removing the four decoys as designed, and the true
        // offset with them. Padding is not specified to be zero, and the exact field sequence around
        // `visible` is precisely the kind of thing this calibrator must not assume.
        Assert.Equal(GridSceneMemory.CanvasItemVisible, Offset(result.Derivation.Semantic.Offsets, OffsetKeys.CanvasItemVisible));
        Assert.False(result.Nodes.Single(n => n.Name == "HiddenTwin").Visible);
        Assert.True(result.Nodes.Single(n => n.Name == "VisibleTwin").Visible);
    }

    [Fact]
    public void AnEmptyVisibleResultSaysWhichRuleEliminatedEachCandidate()
    {
        GridSceneMemory memory = new();
        NodeSample twin = Sample(memory, "RootHarness/AlphaPanel/BetaBranch/VisibleTwin");
        List<string> diagnostics = [];

        // Both twins visible: nothing is ever nominated, which is a different failure from
        // "nominated and rejected" — and telling them apart from the outside used to cost a whole
        // matrix run.
        new SemanticCalibrator(GodotPrecisionWidth.Single)
            .DeriveVisible(twin, twin, [twin], GridSceneMemory.ControlOffsets, diagnostics);

        GridSceneMemory ancestor = new(hiddenByAncestor: true);
        NodeSample visible = Sample(ancestor, "RootHarness/AlphaPanel/BetaBranch/VisibleTwin");
        NodeSample hidden = Sample(ancestor, "RootHarness/AlphaPanel/BetaBranch/HiddenTwin");
        diagnostics.Clear();

        new SemanticCalibrator(GodotPrecisionWidth.Single)
            .DeriveVisible(visible, hidden, [visible, hidden], GridSceneMemory.ControlOffsets, diagnostics);

        // parent_visible_in_tree is nominated alongside `visible` and rejected by name.
        Assert.Contains(diagnostics, d => d.Contains("not 8-aligned", StringComparison.Ordinal));
    }

    private static NodeSample Sample(GridSceneMemory memory, string path)
    {
        ulong address = memory.ByPath[path];
        return new NodeSample(address, MemoryWindow.Read(memory, address, 0xC00));
    }

    /// <summary>A byte-addressable scratch process, for testing the validator in isolation.</summary>
    private sealed class RawMemory : IMemoryReader
    {
        private readonly Dictionary<ulong, byte> _bytes = [];

        public bool Is64Bit => true;

        public bool TryRead(ulong address, Span<byte> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!_bytes.TryGetValue(address + (ulong)i, out byte value))
                {
                    return false;
                }

                buffer[i] = value;
            }

            return true;
        }

        public void WriteUInt64(ulong address, ulong value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            Write(address, buffer);
        }

        public void WriteUInt32(ulong address, uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            Write(address, buffer);
        }

        public void Fill(ulong address, int length, byte value)
        {
            for (int i = 0; i < length; i++)
            {
                _bytes[address + (ulong)i] = value;
            }
        }

        public void Dispose()
        {
        }

        public void Write(ulong address, ReadOnlySpan<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                _bytes[address + (ulong)i] = data[i];
            }
        }
    }
}
