namespace Godot.External.Reflection;

/// <summary>
/// What two independent derivations of the same offset said about each other.
/// </summary>
/// <remarks>
/// <see cref="NoOpinion"/> is not a mild form of <see cref="Disagree"/>. It means one side abstained,
/// which is the normal and correct outcome for a computed getter or an uncalibrated field, and it
/// must never be read as evidence in either direction.
/// </remarks>
public enum OffsetAgreement
{
    /// <summary>At least one side published nothing. No conclusion is available.</summary>
    NoOpinion = 0,

    /// <summary>Both sides published, and published the same value.</summary>
    Agree = 1,

    /// <summary>
    /// Both sides published and the values differ. <b>Neither is trustworthy afterwards</b> — a
    /// disagreement falsifies one of them without saying which.
    /// </summary>
    Disagree = 2,

    /// <summary>
    /// No comparison was attempted, because the getter route could not say <em>whose</em> field it
    /// was looking at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distinct from <see cref="NoOpinion"/> on purpose, and the distinction is the one docs/analysis.md
    /// §13.2 paid for. That table recorded <c>Label::get_text</c> at RVA <c>0x1483bb0</c> decoding
    /// <c>+0x800</c>; a live <c>ClassDB</c> walk later resolved the real <c>Label::get_text</c> to RVA
    /// <c>0x15d11b0</c>, decoding <c>+0x7f8</c>. The original row was a <em>different</em>
    /// <c>String</c> getter that happened to sit at an offset already believed — a decode that agreed
    /// with the answer, from a function nobody had identified.
    /// </para>
    /// <para>
    /// <b>An offset without a name attached is not evidence.</b> So a decode with no attribution does
    /// not get to reach <see cref="Agree"/>, and it is not folded into <see cref="NoOpinion"/> either:
    /// "the decoder abstained" and "we never established which function this was" lead to different
    /// investigations, and only one of them is a property of the engine.
    /// </para>
    /// </remarks>
    NotCompared = 3,
}
