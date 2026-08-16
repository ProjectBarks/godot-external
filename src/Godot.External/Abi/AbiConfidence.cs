namespace Godot.External.Abi;

/// <summary>
/// How much a profile's numbers are actually worth. Published as data because docs/analysis.md §8.9
/// insists on reporting <em>measured</em> coverage rather than a binary "supported".
/// </summary>
/// <remarks>
/// <para>
/// <b>The numeric order is a trust order, and it is load-bearing.</b> A consumer writing
/// <c>profile.Confidence >= AbiConfidence.Calibrated</c> must never be handed a table whose other
/// offsets are unproven, so the ranking is deliberately: nothing &lt; measured-here &lt;
/// measured-end-to-end.
/// </para>
/// <para>
/// <see cref="Calibrated"/> ranks <em>below</em> <see cref="LiveValidated"/> despite being measured
/// against the live target, because calibration is per-offset: §12.5 derived three offsets, and a
/// profile with three derived values and fifteen unproven ones is not equivalent to one where every
/// value was checked against a running game (30/30, 21/21, 60/60). A mixed table is worth what its
/// weakest part is worth.
/// </para>
/// </remarks>
public enum AbiConfidence
{
    /// <summary>
    /// Recorded from disassembly but never checked against a running game. Treat as a hint for
    /// calibration, not as truth. The 4.5.1 debug column is here — §4.6 found its own constants
    /// internally inconsistent (<c>offset[4]</c> at 0x500..0x50c overlaps <c>position</c> at 0x508).
    /// </summary>
    Unvalidated = 0,

    /// <summary>
    /// Derived at connect time by the calibrator (§12.5) rather than looked up. Applies to the
    /// offsets that were actually derived — see <see cref="GodotAbiProfile.IsCalibrated"/> — which is
    /// why a partly calibrated profile cannot claim more than this.
    /// </summary>
    Calibrated = 1,

    /// <summary>Confirmed against a live process. The 4.5.1 release column: 30/30 (§12.3), 21/21 (§12.3b), 60/60 (§12.4c).</summary>
    LiveValidated = 2,
}
