using Godot.External.Reflection;

namespace Godot.External.Tests;

/// <summary>
/// The cross-check: a getter-decoded offset confronted with an independently derived one.
/// </summary>
/// <remarks>
/// <para>
/// The calibrator publishes when exactly one bracketed candidate survives. That is a statement about
/// a search space. Reading the getter's machine code is a statement about the engine, made without
/// touching the heap. Two agreeing derivations that share no inputs are worth far more than either
/// one narrowing to a single survivor, which is why this type exists at all.
/// </para>
/// <para>
/// The disagreement tests are the ones that matter: a cross-check that resolves ties by preferring
/// one side is not a cross-check, it is the same single-source publication with extra ceremony.
/// </para>
/// </remarks>
public sealed class OffsetCrossCheckTests
{
    /// <summary>The 4.5 release <c>String</c> getter at <c>+0x800</c> (RVA <c>0x1483bb0</c>).</summary>
    private static readonly byte[] StringGetter0x800Release =
    [
        0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0xC7, 0x01, 0x00, 0x00, 0x00, 0x00, 0x48, 0x89, 0xCB,
        0x48, 0x8B, 0x8A, 0x00, 0x08, 0x00, 0x00, 0x48, 0x85, 0xC9, 0x74, 0x13, 0x48, 0x89, 0x0B,
        0xE8, 0xAD, 0xDE, 0x6B, 0x01, 0x84, 0xC0, 0x75, 0x07, 0x48, 0xC7, 0x03, 0x00, 0x00, 0x00,
        0x00, 0x48, 0x89, 0xD8, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3,
    ];

    /// <summary>Its 4.5 <b>debug</b> structural twin, at <c>+0x808</c> (RVA <c>0x11bc100</c>).</summary>
    private static readonly byte[] StringGetter0x808Debug =
    [
        0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0xC7, 0x01, 0x00, 0x00, 0x00, 0x00, 0x48, 0x89, 0xCB,
        0x48, 0x8B, 0x8A, 0x08, 0x08, 0x00, 0x00, 0x48, 0x85, 0xC9, 0x74, 0x13, 0x48, 0x89, 0x0B,
        0xE8, 0xFD, 0xE3, 0x76, 0x01, 0x84, 0xC0, 0x75, 0x07, 0x48, 0xC7, 0x03, 0x00, 0x00, 0x00,
        0x00, 0x48, 0x89, 0xD8, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3,
    ];

    /// <summary><c>CanvasItem::is_visible</c>, release: <c>movzx eax, byte [rcx+0x370]; ret</c>.</summary>
    private static readonly byte[] CanvasItemIsVisibleRelease =
        [0x0F, 0xB6, 0x81, 0x70, 0x03, 0x00, 0x00, 0xC3];

    [Fact]
    public void AgreementPublishesTheValue()
    {
        // §4.6's release column for Label.getText is 0x800; the getter route independently says
        // 0x800. This is the shape a caller should require before writing an offset anywhere.
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(StringGetter0x800Release, 0x800);

        Assert.Equal(OffsetAgreement.Agree, result.Agreement);
        Assert.True(result.IsCorroborated);
        Assert.Equal(0x800, result.Offset);
    }

    [Fact]
    public void DisagreementPublishesNothingAtAll()
    {
        // §4.6 records 0x848 as Label.getText's DEBUG offset. The 4.5.0 debug template's only
        // String getter in that neighbourhood is at 0x808 — release + 8, which is exactly the
        // uniform shift §12.7 measured independently with the ABI grid. So the two routes
        // disagree with §4.6, and this API's response to a disagreement is silence: not the
        // decoded value, not the recorded one.
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(StringGetter0x808Debug, 0x848);

        Assert.Equal(OffsetAgreement.Disagree, result.Agreement);
        Assert.False(result.IsCorroborated);
        Assert.Null(result.Offset);
        Assert.Contains("0x808", result.Reason, StringComparison.Ordinal);
        Assert.Contains("0x848", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDebugTwinAgreesWithTheGridsUniformShift()
    {
        // The other half of the same finding: §12.7's "debug is release + 8" predicts 0x808 for a
        // release field at 0x800, and the getter route lands on it.
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(StringGetter0x808Debug, 0x800 + 0x8);

        Assert.Equal(OffsetAgreement.Agree, result.Agreement);
        Assert.Equal(0x808, result.Offset);
    }

    [Fact]
    public void OneSideAbstainingIsNotCorroboration()
    {
        // A single route is a hypothesis. Silence from the other side does not upgrade it.
        OffsetCrossCheckResult noBracket = OffsetCrossCheck.Compare(CanvasItemIsVisibleRelease, null);

        Assert.Equal(OffsetAgreement.NoOpinion, noBracket.Agreement);
        Assert.Null(noBracket.Offset);
        Assert.False(noBracket.IsCorroborated);
    }

    [Fact]
    public void ARefusedDecodeCannotCorroborateAnything()
    {
        // is_visible_in_tree: two fields, no attribution. Even against the "right" bracketed answer
        // this must stay NoOpinion — otherwise a refusal that happens to sit next to a correct
        // number would launder itself into agreement.
        byte[] ambiguous =
        [
            0x0F, 0xB6, 0x81, 0x70, 0x03, 0x00, 0x00, 0x84, 0xC0, 0x74, 0x07, 0x0F, 0xB6, 0x81,
            0x71, 0x03, 0x00, 0x00, 0xC3,
        ];

        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(ambiguous, 0x370);

        Assert.Equal(OffsetAgreement.NoOpinion, result.Agreement);
        Assert.Null(result.Offset);
        Assert.Contains(nameof(FieldOffsetDecodeStatus.AmbiguousAccesses), result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseColumnForCanvasItemVisibleIsCorroborated()
    {
        // §4.6 and §12.7 both give 0x370 for the 4.5 release build, and this getter — identified by
        // name through its _bind_methods reference to the "is_visible" string — is a third,
        // independent route to the same number.
        Assert.True(OffsetCrossCheck.Compare(CanvasItemIsVisibleRelease, 0x370).IsCorroborated);
    }

    // ---------------------------------------------------------------- named comparison

    private static GetterAttribution Named(string className = "CanvasItem", string method = "is_visible")
        => new()
        {
            ClassName = className,
            MethodName = method,
            MethodBindAddress = 0x25297e85080,
            CodeAddress = 0x7ff625065520,
            CodeRva = 0x139f520,
            Evidence = "bind names verified by value",
        };

    [Fact]
    public void AnUnattributedDecodeCannotAgreeEvenWithTheExactRightNumber()
    {
        // THE test for this type. §13.2 recorded Label::get_text at RVA 0x1483bb0 decoding +0x800 —
        // a decode that agreed with every other source on record, from a function nobody had
        // identified, and it was a DIFFERENT String getter sitting at an offset already believed.
        // Handing the comparison the number it "should" produce must not be enough.
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(StringGetter0x800Release),
            0x800,
            attribution: null);

        Assert.Equal(OffsetAgreement.NotCompared, result.Agreement);
        Assert.False(result.IsCorroborated);
        Assert.Null(result.Offset);
    }

    [Theory]
    [InlineData("", "is_visible")]
    [InlineData("CanvasItem", "")]
    [InlineData("   ", "  ")]
    public void AHalfFilledAttributionIsNotAName(string className, string method)
    {
        // A class with no method names a class, not a getter; a method with no class names a method on
        // no particular type. Neither is an attribution, and neither may reach Agree.
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease),
            0x370,
            Named(className, method));

        Assert.Equal(OffsetAgreement.NotCompared, result.Agreement);
        Assert.Null(result.Offset);
    }

    [Fact]
    public void AnAttributionWithNoCodeAddressNamesNothingThatWasDecoded()
    {
        GetterAttribution nowhere = Named() with { CodeAddress = 0 };

        Assert.False(nowhere.IsNamed);
        Assert.Equal(
            OffsetAgreement.NotCompared,
            OffsetCrossCheck.Compare(GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease), 0x370, nowhere).Agreement);
    }

    [Fact]
    public void ANamedAgreementCarriesTheNameItWasProvedAgainst()
    {
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease),
            0x370,
            Named());

        Assert.Equal(OffsetAgreement.Agree, result.Agreement);
        Assert.Equal(0x370, result.Offset);

        // The name has to travel WITH the verdict. A corroboration whose provenance lives only in the
        // caller's head is one refactor away from being a bare number again.
        Assert.Contains("CanvasItem::is_visible", result.Reason, StringComparison.Ordinal);
        Assert.Contains("0x139f520", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANamedDisagreementStillPublishesNeitherSide()
    {
        // Attaching a name raises what an agreement is worth; it does not soften a disagreement.
        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease),
            0x378,
            Named());

        Assert.Equal(OffsetAgreement.Disagree, result.Agreement);
        Assert.Null(result.Offset);
        Assert.False(result.IsCorroborated);
    }

    [Fact]
    public void NotComparedIsNotTheSameAnswerAsNoOpinion()
    {
        // "The decoder abstained" and "we never established which function this was" lead to different
        // investigations, and only the first is a fact about the engine. Collapsing them would let a
        // caller read one silence for the other — which is the reading this whole task forbids.
        OffsetCrossCheckResult abstained = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease),
            null,
            Named());
        OffsetCrossCheckResult unnamed = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease),
            0x370,
            attribution: null);

        Assert.Equal(OffsetAgreement.NoOpinion, abstained.Agreement);
        Assert.Equal(OffsetAgreement.NotCompared, unnamed.Agreement);
        Assert.NotEqual(abstained.Agreement, unnamed.Agreement);
        Assert.Null(abstained.Offset);
        Assert.Null(unnamed.Offset);
    }

    [Fact]
    public void ARefusedDecodeWithAGoodNameIsStillNoCorroboration()
    {
        // The name is necessary, not sufficient. Every field on a debug template lands here: the
        // bind resolves by name, and the decoder correctly refuses the spilled-`this` body (§16.2).
        byte[] computed = [0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00, 0xC3];

        OffsetCrossCheckResult result = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode(computed),
            0x370,
            Named());

        Assert.NotEqual(OffsetAgreement.Agree, result.Agreement);
        Assert.Null(result.Offset);
    }
}
