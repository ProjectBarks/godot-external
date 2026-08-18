using System.Globalization;

namespace Godot.External.Reflection;

/// <summary>
/// Confronts a getter-decoded offset with an offset derived some other way — normally the
/// calibrator's bracketed candidate — and publishes a value only when the two <b>independent</b>
/// derivations agree.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the point of the whole module.</b> The calibrator publishes when exactly one candidate
/// survives bracketing, which is a statement about a search space, not about the engine. Reading the
/// getter's machine code is a statement about the engine that never looks at the heap at all. The two
/// share no inputs, no assumptions and no failure modes, so two agreeing derivations are worth far
/// more than either one narrowing to a single survivor.
/// </para>
/// <para>
/// <b>A disagreement publishes nothing.</b> Not the decoded value, not the bracketed one. This is
/// the same rule <c>AbsentNeverWrongTests</c> enforces on the calibrator's own tie-breaks: when there
/// is nothing to prefer between two answers, returning either is how a wrong number reaches a
/// published result.
/// </para>
/// <para>
/// Worked example, run against the shipped 4.5-stable Windows templates. docs/analysis.md §4.6
/// records <c>Label.getText</c> as <c>0x800</c> release / <c>0x848</c> debug. In the release
/// template there is exactly one <c>String</c>-returning getter loading <c>[rdx+0x800]</c> — agree.
/// In the debug template there is <b>no</b> <c>String</c> getter at <c>0x848</c> at all, while the
/// byte-for-byte structural twin of the release getter sits at <c>0x808</c>; the getter route
/// therefore disagrees with §4.6's debug column and agrees with §12.7's independently measured
/// "debug is release + 8".
/// </para>
/// </remarks>
public static class OffsetCrossCheck
{
    /// <summary>
    /// Compares a decode result against an independently derived offset.
    /// </summary>
    /// <param name="decoded">The getter-code result. A refusal yields <see cref="OffsetAgreement.NoOpinion"/>.</param>
    /// <param name="independent">
    /// The other derivation — a calibrated/bracketed offset, or <see langword="null"/> when that side
    /// abstained too.
    /// </param>
    /// <returns>The verdict, carrying a value only on <see cref="OffsetAgreement.Agree"/>.</returns>
    public static OffsetCrossCheckResult Compare(FieldOffsetDecodeResult decoded, int? independent)
    {
        ArgumentNullException.ThrowIfNull(decoded);

        if (!decoded.IsDecoded)
        {
            return new OffsetCrossCheckResult(
                OffsetAgreement.NoOpinion,
                null,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "getter route abstained ({0}: {1})",
                    decoded.Status,
                    decoded.Reason));
        }

        if (independent is not { } other)
        {
            return new OffsetCrossCheckResult(
                OffsetAgreement.NoOpinion,
                null,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "getter route says +0x{0:x} but the independent derivation abstained; one route is " +
                    "not corroboration",
                    decoded.Offset));
        }

        int mine = decoded.Offset!.Value;

        return mine == other
            ? new OffsetCrossCheckResult(
                OffsetAgreement.Agree,
                mine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "two independent derivations agree on +0x{0:x} ({1})",
                    mine,
                    decoded.Shape))
            : new OffsetCrossCheckResult(
                OffsetAgreement.Disagree,
                null,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "getter route says +0x{0:x}, independent derivation says +0x{1:x}; one of them is " +
                    "wrong and nothing is published",
                    mine,
                    other));
    }

    /// <summary>
    /// Convenience overload for the common shape: decode the bytes, then compare.
    /// </summary>
    /// <param name="getterBody">Bytes at the getter's entry point.</param>
    /// <param name="independent">The independently derived offset, or <see langword="null"/>.</param>
    /// <param name="options">Decoder bounds; defaults apply when omitted.</param>
    public static OffsetCrossCheckResult Compare(
        ReadOnlySpan<byte> getterBody,
        int? independent,
        GetterDecoderOptions? options = null)
        => Compare(GetterFieldDecoder.Decode(getterBody, options), independent);

    /// <summary>
    /// The comparison a caller should actually use: identical to
    /// <see cref="Compare(FieldOffsetDecodeResult, int?)"/> except that it <b>refuses to reach a
    /// verdict at all</b> unless the decoded body has been identified as a named engine method.
    /// </summary>
    /// <param name="decoded">The getter-code result.</param>
    /// <param name="independent">The other derivation, or <see langword="null"/> when it abstained.</param>
    /// <param name="attribution">
    /// Which method's body was decoded. <see langword="null"/>, or an attribution failing
    /// <see cref="GetterAttribution.IsNamed"/>, yields <see cref="OffsetAgreement.NotCompared"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>The unnamed overloads are not a shortcut for this one.</b> docs/analysis.md §13.2 recorded a
    /// <c>Label::get_text</c> row that decoded the offset everything else agreed on and was still a
    /// different function. A decode that matches a number already believed, from a body nobody
    /// identified, is not corroboration — so this returns <see cref="OffsetAgreement.NotCompared"/>
    /// <em>before</em> looking at either value, and a caller cannot get an <see cref="OffsetAgreement.Agree"/>
    /// out of it by supplying the right number.
    /// </para>
    /// <para>
    /// The name check runs first for the same reason: were it applied afterwards, an unattributed
    /// decode that happened to match would be visible in the reason string as "agreed, but", and the
    /// history of this project says that reads as agreement.
    /// </para>
    /// </remarks>
    public static OffsetCrossCheckResult Compare(
        FieldOffsetDecodeResult decoded,
        int? independent,
        GetterAttribution? attribution)
    {
        ArgumentNullException.ThrowIfNull(decoded);

        if (attribution is not { IsNamed: true })
        {
            return new OffsetCrossCheckResult(
                OffsetAgreement.NotCompared,
                null,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "no comparison: the getter route produced no NAMED method for this field ({0}). An offset "
                    + "without a name attached is not evidence — §13.2's Label::get_text row decoded the value "
                    + "everything else agreed on and was a different function",
                    DescribeMissingName(attribution)));
        }

        OffsetCrossCheckResult verdict = Compare(decoded, independent);

        return verdict with
        {
            Reason = string.Format(
                CultureInfo.InvariantCulture,
                "{0} (getter {1} at RVA 0x{2:x}{3}): {4}",
                attribution,
                attribution.MethodBindAddress == 0
                    ? "resolved by name"
                    : string.Format(CultureInfo.InvariantCulture, "from MethodBind 0x{0:x}", attribution.MethodBindAddress),
                attribution.CodeRva,
                attribution.Evidence.Length == 0 ? string.Empty : "; " + attribution.Evidence,
                verdict.Reason),
        };
    }

    private static string DescribeMissingName(GetterAttribution? attribution) => attribution is null
        ? "no attribution supplied at all"
        : string.Format(
            CultureInfo.InvariantCulture,
            "class=\"{0}\", method=\"{1}\", code=0x{2:x}",
            attribution.ClassName,
            attribution.MethodName,
            attribution.CodeAddress);
}
