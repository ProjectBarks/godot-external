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
}
