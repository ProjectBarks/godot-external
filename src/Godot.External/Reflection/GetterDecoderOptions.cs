namespace Godot.External.Reflection;

/// <summary>
/// The bounds <see cref="GetterFieldDecoder"/> works inside. Every one of them is a refusal
/// criterion, so widening any of them trades safety for coverage — deliberately, and at a call site
/// rather than silently in the decoder.
/// </summary>
/// <remarks>
/// The defaults are the values that produced the validated result on the shipped 4.5-stable Windows
/// x86-64 templates: 3/3 on <c>CanvasItem::is_visible</c> (<c>+0x370</c>), <c>Label::get_text</c>
/// (<c>+0x7f8</c>) and <c>RichTextLabel::get_text</c> (<c>+0xa78</c>), all three now resolved
/// <b>by name</b> through a live <c>ClassDB</c> walk rather than by matching an offset.
/// <para>
/// The <c>Label::get_text</c> row previously recorded here as RVA <c>0x1483bb0</c> decoding
/// <c>+0x800</c> was wrong and is deleted: <c>0x1483bb0</c> is a different <c>String</c> getter that
/// merely sat at an offset already believed. Godot 4.5's real <c>Label::get_text</c> is at RVA
/// <c>0x15d11b0</c> and decodes <c>+0x7f8</c> (docs/analysis.md §13.2, §16).
/// </para>
/// </remarks>
public sealed class GetterDecoderOptions
{
    /// <summary>
    /// The instance used when a caller passes none. Immutable in practice — treat it as a constant.
    /// </summary>
    public static GetterDecoderOptions Default { get; } = new();

    /// <summary>
    /// How many bytes of the body to inspect, default <c>0x40</c>.
    /// </summary>
    /// <remarks>
    /// The three validated getters run 8, 0x37 and 0x3e bytes to their <c>ret</c>, so 0x40 covers
    /// them with almost nothing to spare — which is the point. A larger window does not decode more
    /// <em>trivial</em> getters, it only admits larger functions whose returns the decoder cannot
    /// reason about.
    /// </remarks>
    public int WindowBytes { get; init; } = 0x40;

    /// <summary>
    /// Smallest accepted displacement, default <c>0x20</c>.
    /// </summary>
    /// <remarks>
    /// Below this lies header territory that every <c>Object</c> shares — the vtable pointer at 0,
    /// the CowData <c>[refcount][size]</c> words, and the hidden-return buffer that MSVC zeroes with
    /// <c>mov qword ptr [rcx], 0</c> at the top of every <c>String</c> getter. That last one is why
    /// the floor is not optional: without it, every String getter would report a second candidate
    /// access at +0 and refuse as ambiguous.
    /// </remarks>
    public int MinimumDisplacement { get; init; } = 0x20;

    /// <summary>
    /// Largest accepted displacement, default <c>0x10000</c>.
    /// </summary>
    /// <remarks>
    /// Godot's largest engine classes measure a few kilobytes (the deepest offset in the shipped 4.5
    /// templates that this work touched is <c>RichTextLabel</c>'s text at <c>0xa78</c>), so anything
    /// past 64 KB is a table index or a bad base register, not a member.
    /// </remarks>
    public int MaximumDisplacement { get; init; } = 0x10000;

    /// <summary>
    /// The ABI the bytes were compiled under. Anything but
    /// <see cref="NativeCallingConvention.MsvcX64"/> is refused rather than guessed at.
    /// </summary>
    public NativeCallingConvention CallingConvention { get; init; } = NativeCallingConvention.MsvcX64;

    /// <summary>
    /// When <see langword="true"/>, refuse any body that calls or tail-jumps out — i.e. accept only
    /// <see cref="GetterShape.LeafLoad"/>. Default <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <b>Setting this rejects every <c>String</c> property in the engine</b>, including
    /// <c>Label::text</c>, because copy-on-write returns always call a reference-count helper. It
    /// exists for the case where a caller wants the strictest possible evidence for a scalar field
    /// and would rather have nothing than an inference.
    /// </remarks>
    public bool RequireLeafBody { get; init; }
}
