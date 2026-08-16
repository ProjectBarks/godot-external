namespace Godot.External.Abi;

/// <summary>
/// Width of the engine's <c>real_t</c>. A double-precision build widens every <c>real_t</c> field,
/// which shifts every offset after the first one — docs/analysis.md §8.9 lists precision as its own
/// axis of the compat matrix for exactly that reason.
/// </summary>
public enum GodotPrecision
{
    /// <summary><c>real_t == float</c> (4 bytes). Godot's default, and the only measured cell.</summary>
    Single = 0,

    /// <summary>
    /// <c>real_t == double</c> (8 bytes). No offset table exists for this; a double-precision target
    /// must be calibrated (§12.5) rather than looked up.
    /// </summary>
    Double = 1,
}
