namespace Godot.External.Abi;

/// <summary>
/// Which export template the target game was built with. This — not the engine version — is what
/// selects between the two field layouts scry branches on.
/// </summary>
/// <remarks>
/// docs/analysis.md §4.6 settles this: every one of the twelve accessors emits
/// <c>lVar = &lt;debug const&gt;; if (*(char *)(engine[1] + 0x3c) == '\0') lVar = &lt;release const&gt;;</c>,
/// and the version parser (<c>FUN_1800422d0</c>) sets that flag byte to 1 <em>only</em> when the
/// supplied version string ends in <c>-debug</c>. Debug templates carry extra fields, which is why
/// the two columns differ by roughly 0x50 bytes.
/// </remarks>
public enum GodotBuildTemplate
{
    /// <summary>Release export template — flag byte 0. The validated column (§12.3, §12.4c).</summary>
    Release = 0,

    /// <summary>
    /// Debug export template — flag byte 1. Measured by the ABI grid; see
    /// <see cref="GodotAbiProfiles.Godot451Debug"/>.
    /// </summary>
    Debug = 1,
}
