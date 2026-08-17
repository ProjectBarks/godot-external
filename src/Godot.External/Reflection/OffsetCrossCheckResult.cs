using System.Globalization;

namespace Godot.External.Reflection;

/// <summary>
/// The verdict of an <see cref="OffsetCrossCheck"/>, with a value attached only when two independent
/// derivations agreed.
/// </summary>
/// <param name="Agreement">Whether the two routes corroborated each other.</param>
/// <param name="Offset">
/// The corroborated offset. <see langword="null"/> for both <see cref="OffsetAgreement.NoOpinion"/>
/// and <see cref="OffsetAgreement.Disagree"/> — a disagreement does not get to pick a winner.
/// </param>
/// <param name="Reason">Why, in words. Always populated; this is the line worth logging.</param>
public readonly record struct OffsetCrossCheckResult(OffsetAgreement Agreement, int? Offset, string Reason)
{
    /// <summary>
    /// <see langword="true"/> only when both routes published and matched. This is the one condition
    /// under which an offset from this module may be written into a profile.
    /// </summary>
    public bool IsCorroborated => Agreement == OffsetAgreement.Agree;

    /// <inheritdoc/>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0}: {1}",
        Agreement,
        Reason);
}
