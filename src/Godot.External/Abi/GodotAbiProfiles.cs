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
        Notes = "StS2 build [v0.107.1] (2026.06.18); 30/30 §12.3, 21/21 §12.3b, 60/60 §12.4c.",
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
            LabelText = 0x800,
            RichTextLabelText = 0xa78,
        },
    };

    /// <summary>
    /// <b>Godot 4.5.1, debug export template — UNVALIDATED, and known to be internally inconsistent.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recorded from the debug branch of scry's accessors so the calibrator has a starting guess,
    /// and so the §8.9 grid has something to diff against. It has never been checked against a
    /// running debug-template build.
    /// </para>
    /// <para>
    /// <b>The numbers cannot all be right.</b> §4.6 confirmed in the disassembly that
    /// <c>getOffset</c> reads <c>0x500..0x50c</c> while <c>getPosition</c> reads
    /// <c>0x508</c>/<c>0x50c</c> — a genuine overlap in scry's own debug constants, not a misreading.
    /// <c>offset[2]</c>/<c>offset[3]</c> and <c>pos_cache</c> cannot both live at those addresses.
    /// The debug path is presumably untested upstream. Treat this profile as a hint only, and
    /// prefer calibration (§12.5) for any debug-template target.
    /// </para>
    /// </remarks>
    public static GodotAbiProfile Godot451DebugUnvalidated { get; } = new()
    {
        EngineVersion = "4.5.1",
        Template = GodotBuildTemplate.Debug,
        Precision = GodotPrecision.Single,
        Confidence = AbiConfidence.Unvalidated,
        Notes = "Debug column from §4.6. NEVER live-validated, and self-inconsistent: "
              + "ControlOffsets spans 0x500..0x50c which overlaps ControlPosition at 0x508. "
              + "Calibrate before use.",
        Offsets = new GodotOffsetTable
        {
            CanvasItemVisible = 0x3c0,
            ControlGlobalPosition = 0x448,
            ControlOffsets = 0x500,
            ControlScale = 0x4f8,
            ControlPosition = 0x508,
            ControlSize = 0x510,
            NodeParent = 0x178,
            NodeChildListHead = 0x198,
            NodeName = 0x210,
            NodeScriptInstance = 0x70,
            LabelText = 0x848,
            RichTextLabelText = 0xb18,
        },
    };

    /// <summary>All shipped profiles, validated and not.</summary>
    public static IReadOnlyList<GodotAbiProfile> All { get; } =
    [
        Godot451Release,
        Godot451DebugUnvalidated,
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
