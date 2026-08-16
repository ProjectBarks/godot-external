namespace Godot.External.Scene;

/// <summary>
/// As much node type identity as this layer needs: is it a <c>Control</c>, or not?
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a full class hierarchy. docs/analysis.md §4.6 records that the Godot layer has
/// "almost no RTTI" — thin non-polymorphic structs over version-keyed offsets — and §12.4c shows why
/// that is dangerous: <c>engine.getControl()</c> on an <c>AudioStreamPlayer</c> "returns denormal
/// garbage such as <c>2.6e-38</c>" with no error, because <b>there is no type check</b>. The doc's
/// own conclusion is "validate by plausibility or by class name".
/// </para>
/// <para>
/// The one question that must be answerable is the one the global-position composition depends on:
/// summing <c>Control::Data::pos_cache</c> up the parent chain is only meaningful while the
/// ancestors are Controls. Everything richer than that belongs to a caller that can read managed
/// class names (§12.4d).
/// </para>
/// </remarks>
internal enum GodotNodeClass
{
    /// <summary>
    /// Could not be determined — typically a failed read. Treated as <em>not</em> a Control wherever
    /// a decision is forced, because guessing yes is what produces the denormal garbage above.
    /// </summary>
    Unknown = 0,

    /// <summary>Definitely not a <c>Control</c>: reading Control geometry off it would return garbage.</summary>
    NotControl = 1,

    /// <summary>A <c>Control</c> (or a subclass — <c>Label</c>, <c>RichTextLabel</c>, …).</summary>
    Control = 2,
}
