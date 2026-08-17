using System.Diagnostics.CodeAnalysis;

namespace Godot.External.Abi;

/// <summary>
/// The shipped profiles, and the version-string rule that picks between them.
/// </summary>
/// <remarks>
/// Exactly one cell of the compat matrix has been measured — Godot 4.5.1, release template, single
/// precision, one modified engine (docs/analysis.md §8.9). Everything else here is either recorded
/// from disassembly and unvalidated, or absent. Do not read this class as "supported versions".
/// </remarks>
public static class GodotAbiProfiles
{
    /// <summary>
    /// The suffix scry's version parser (<c>FUN_1800422d0</c>) strips, and whose presence sets the
    /// debug-template flag at <c>engine[1] + 0x3c</c> (§4.6).
    /// </summary>
    public const string DebugSuffix = "-debug";

    /// <summary>
    /// <b>Godot 4.5.1, release export template, single precision — live-validated.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every scalar offset here was confirmed against a running game: 30/30 across five menu
    /// Controls (§12.3), 21/21 on node-level and Label offsets (§12.3b), and 60/60 across thirteen
    /// combat-UI nodes (§12.4c). Distinctive values matched exactly — <c>BgContainer</c> size
    /// <c>[2560,1200]</c>, offsets <c>[-960,-516,1600,684]</c>.
    /// </para>
    /// <para>
    /// Two independent sources agree on the shape: the release column is ascending and
    /// non-overlapping, and reproduces upstream <c>Control::Data</c> field ordering
    /// (<c>offset[4]</c> → <c>anchor[4]</c> → focus/grow enums → rotation → <c>scale</c> → …
    /// → <c>pos_cache</c> → <c>size_cache</c>) with 4-byte <c>real_t</c>.
    /// </para>
    /// <para>
    /// Valid <em>for that build</em>: the game reported <c>[v0.107.1] (2026.06.18)</c> (§12.3b).
    /// The engine is a customised 4.5.1, not a stock export template, so these numbers are not
    /// automatically right for any other 4.5.1 game (§8.9, "Honest limit").
    /// </para>
    /// </remarks>
    public static GodotAbiProfile Godot451Release { get; } = new()
    {
        EngineVersion = "4.5.1",
        Template = GodotBuildTemplate.Release,
        Precision = GodotPrecision.Single,
        Confidence = AbiConfidence.LiveValidated,
        Notes = "StS2 build [v0.107.1] (2026.06.18); 30/30 §12.3, 21/21 §12.3b, 60/60 §12.4c. "
              + "LabelText corrected 0x800 -> 0x7f8 from the ABI grid (2026-08-17): 0x800 is xl_text, the "
              + "translated copy Label stores immediately after text, and the two share one allocation.",
        Offsets = new GodotOffsetTable
        {
            CanvasItemVisible = 0x370,
            ControlGlobalPosition = 0x3f8, // cached and often stale — see GodotOffsetTable docs
            ControlOffsets = 0x470,
            ControlScale = 0x4a8,
            ControlPosition = 0x4b8,
            ControlSize = 0x4c0,
            NodeParent = 0x128,
            NodeChildListHead = 0x148,
            NodeName = 0x1c0,
            NodeScriptInstance = 0x68, // ScriptInstance*, NOT the managed object
            LabelText = 0x7f8,
            RichTextLabelText = 0xa78,
        },
    };

    /// <summary>
    /// <b>Godot 4.5.1, debug export template, single precision — grid-measured.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the §4.6 debug column, which was transcribed from scry's debug branch, was never
    /// live-validated, and was internally inconsistent — it had <c>getOffset</c> spanning
    /// <c>0x500..0x50c</c> while <c>getPosition</c> read <c>0x508</c>, which cannot both be true. The
    /// ABI grid derived this column instead, from stock 4.5 debug export templates, and got the same
    /// values on three passes over unchanged binaries with no contradictions.
    /// </para>
    /// <para>
    /// The measured relationship is simply <b>debug = release + 8</b>, uniformly across every field.
    /// The old column's ~0x48–0x50 deltas were not a different layout; they were wrong.
    /// </para>
    /// </remarks>
    public static GodotAbiProfile Godot451Debug { get; } = new()
    {
        EngineVersion = "4.5.1",
        Template = GodotBuildTemplate.Debug,
        Precision = GodotPrecision.Single,
        Confidence = AbiConfidence.Calibrated,
        Notes = "Derived by the ABI grid from stock 4.5 debug export templates (2026-08-17), three passes, "
              + "no contradictions. Supersedes the §4.6 debug column, which was unvalidated and "
              + "self-inconsistent. Uniformly release + 8.",
        Offsets = new GodotOffsetTable
        {
            CanvasItemVisible = 0x378,
            ControlGlobalPosition = 0x400,
            ControlOffsets = 0x478,
            ControlScale = 0x4b0,
            ControlPosition = 0x4c0,
            ControlSize = 0x4c8,
            NodeParent = 0x130,
            NodeChildListHead = 0x150,
            NodeName = 0x1c8,
            NodeScriptInstance = 0x70,
            LabelText = 0x800,
            RichTextLabelText = 0xa80,
        },
    };

    /// <summary>All shipped profiles, validated and not.</summary>
    public static IReadOnlyList<GodotAbiProfile> All { get; } =
    [
        Godot451Release,
        Godot451Debug,
    ];

    /// <summary>
    /// Splits a Godot version string into version and template the way the engine's own consumer
    /// does: a trailing <c>-debug</c> means the debug export template, anything else means release
    /// (§4.6). The version number itself does <em>not</em> select the layout.
    /// </summary>
    public static (string Version, GodotBuildTemplate Template) ParseVersionString(string versionString)
    {
        ArgumentNullException.ThrowIfNull(versionString);

        string trimmed = versionString.Trim();
        return trimmed.EndsWith(DebugSuffix, StringComparison.OrdinalIgnoreCase)
            ? (trimmed[..^DebugSuffix.Length], GodotBuildTemplate.Debug)
            : (trimmed, GodotBuildTemplate.Release);
    }

    /// <summary>
    /// Looks up a shipped profile. Returns <see langword="false"/> when the cell has not been
    /// measured — which is the common case, and is a cue to calibrate, not to guess a neighbour.
    /// </summary>
    public static bool TryGet(
        string versionString,
        GodotPrecision precision,
        [NotNullWhen(true)] out GodotAbiProfile? profile)
    {
        (string version, GodotBuildTemplate template) = ParseVersionString(versionString);
        return TryGet(version, template, precision, out profile);
    }

    /// <inheritdoc cref="TryGet(string, GodotPrecision, out GodotAbiProfile)"/>
    public static bool TryGet(
        string version,
        GodotBuildTemplate template,
        GodotPrecision precision,
        [NotNullWhen(true)] out GodotAbiProfile? profile)
    {
        foreach (GodotAbiProfile candidate in All)
        {
            if (candidate.Template == template
                && candidate.Precision == precision
                && string.Equals(candidate.EngineVersion, version, StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                return true;
            }
        }

        profile = null;
        return false;
    }
}
