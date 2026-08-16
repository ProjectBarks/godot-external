using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Objects;

/// <summary>A Godot <c>RichTextLabel</c>.</summary>
/// <remarks>
/// Mechanically identical to <see cref="GodotLabel"/> but at a different offset —
/// <c>0xa78</c> versus <c>0x800</c> on the validated release profile (docs/analysis.md §4.6). That is
/// the whole reason this is a separate type rather than a flag: reading a <c>RichTextLabel</c> through
/// <see cref="GodotLabel"/> would dereference whatever happens to live at <c>0x800</c> on it, which
/// the engine will not object to.
/// </remarks>
internal sealed class GodotRichTextLabel : GodotControl
{
    internal GodotRichTextLabel(SceneEpoch epoch, NativePtr address)
        : base(epoch, address)
    {
    }

    /// <summary>Reads <c>RichTextLabel::text</c>. See <see cref="GodotLabel.TryGetText"/> for the mechanism.</summary>
    public bool TryGetText(out string text)
        => GodotString.TryReadField(Source, Profile, Address.Address, GodotField.RichTextLabelText, out text);

    /// <summary>The label's text, or <see langword="null"/> on a failed read.</summary>
    public string? Text => TryGetText(out string text) ? text : null;
}
