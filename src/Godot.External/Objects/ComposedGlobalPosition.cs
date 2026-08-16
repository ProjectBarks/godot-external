using Godot.External.Values;

namespace Godot.External.Objects;

/// <summary>
/// A global position obtained by <b>composing</b> local positions up the parent chain, plus the
/// evidence about how far that composition got.
/// </summary>
/// <remarks>
/// <para>
/// docs/analysis.md §4.6 settles why composition is necessary: <c>getGlobalPosition</c>
/// (<c>FUN_180012c70</c>) "performs exactly two <c>readFloat</c> calls … and no arithmetic" — it is a
/// cached field, not a transform. Live it returned <c>[0,0]</c> for <c>MainMenuTextButtons</c> and
/// <c>ContinueButton</c> while both had real on-screen positions (§12.3). Summing local positions,
/// as scry's own <c>computeGlobalPosition</c> does, is the correct approach.
/// </para>
/// <para>
/// <see cref="StoppedAtNonControl"/> is reported rather than hidden because it changes what the
/// number means. The sum is relative to the first non-<c>Control</c> ancestor, which for a UI rooted
/// under a <c>Node2D</c> or a <c>CanvasLayer</c> is usually the screen — but not always, and a caller
/// that needs certainty needs to know it stopped.
/// </para>
/// </remarks>
/// <param name="Position">The composed position.</param>
/// <param name="AncestorsComposed">How many ancestors contributed.</param>
/// <param name="StoppedAtNonControl">
/// <see langword="true"/> when the walk halted because the next ancestor was not a <c>Control</c>,
/// rather than because it reached a root.
/// </param>
internal readonly record struct ComposedGlobalPosition(
    GodotVector2 Position,
    int AncestorsComposed,
    bool StoppedAtNonControl)
{
    /// <inheritdoc/>
    public override string ToString()
        => $"{Position} (+{AncestorsComposed} ancestors{(StoppedAtNonControl ? ", stopped at non-Control" : string.Empty)})";
}
