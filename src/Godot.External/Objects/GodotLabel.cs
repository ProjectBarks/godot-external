using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Objects;

/// <summary>A Godot <c>Label</c> — a <c>Control</c> that also has text.</summary>
/// <remarks>
/// docs/analysis.md §12.3b validated this path 5/5 live, recovering <c>"Controller Detected"</c>,
/// <c>"Connection Interrupted"</c> and the game's own build string
/// <c>"[v0.107.1] (2026.06.18)"</c> — which is worth remembering, because every offset in the shipped
/// profile is valid <em>for that build</em>.
/// </remarks>
internal sealed class GodotLabel : GodotControl
{
    internal GodotLabel(SceneEpoch epoch, NativePtr address)
        : base(epoch, address)
    {
    }

    /// <summary>Reads <c>Label::text</c>.</summary>
    /// <remarks>
    /// <para>
    /// The field holds a <c>CowData&lt;char32_t&gt;</c> <b>data</b> pointer, and CowData stores
    /// <c>[refcount][size]</c> <em>ahead</em> of the data — so the length is read at
    /// <c>ptr - CowDataSizeBackOffset</c>, not at the pointer (§4.6). A null pointer is a legitimately
    /// empty string, not a failure.
    /// </para>
    /// <para>
    /// Decoding is real UTF-32 in a single bulk read. §4.6 records that scry truncates each
    /// <c>char32_t</c> to a byte — "fine for ASCII, lossy for anything else" — and that its
    /// <c>getName</c> path performs one remote read per character, which is both slower and a wider
    /// window for the target to mutate underneath the read.
    /// </para>
    /// </remarks>
    public bool TryGetText(out string text)
        => GodotString.TryReadField(Source, Profile, Address.Address, GodotField.LabelText, out text);

    /// <summary>The label's text, or <see langword="null"/> on a failed read.</summary>
    public string? Text => TryGetText(out string text) ? text : null;
}
