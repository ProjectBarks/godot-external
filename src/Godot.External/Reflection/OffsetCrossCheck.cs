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
}
