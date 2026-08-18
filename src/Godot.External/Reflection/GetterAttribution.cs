using System.Globalization;

namespace Godot.External.Reflection;

/// <summary>
/// Which engine method a decoded getter body actually <em>was</em> — the name that turns a decoded
/// displacement into evidence.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type exists because of one specific mistake.</b> docs/analysis.md §13.2 recorded
/// <c>Label::get_text</c> at RVA <c>0x1483bb0</c> decoding <c>+0x800</c>, and the number agreed with
/// everything else on record. It was still wrong: a live <c>ClassDB</c> walk resolved the real
/// <c>Label::get_text</c> by name to RVA <c>0x15d11b0</c>, decoding <c>+0x7f8</c>. <c>0x1483bb0</c>
/// was some other <c>String</c> getter that happened to sit at an offset already believed. Matching a
/// number you already hold is confirmation bias with extra steps, and the only thing that separates
/// the two cases is whether the function was <em>identified</em>.
/// </para>
/// <para>
/// So an attribution is not decoration on the result — it is the precondition for the result being
/// allowed to say <see cref="OffsetAgreement.Agree"/> at all. See
/// <see cref="OffsetCrossCheck.Compare(FieldOffsetDecodeResult, int?, GetterAttribution?)"/>, which
/// returns <see cref="OffsetAgreement.NotCompared"/> when there is no name.
/// </para>
/// <para>
/// <b>What a legitimate attribution costs.</b> The only route that produces one here is the live
/// <c>ClassDB</c> walk: the class's own <c>method_map</c> is found by walking the intrusive chains
/// inside its <c>ClassInfo</c>, the entry is matched by key <em>name</em>, and the resulting
/// <c>MethodBind</c> is then re-verified by reading its own <c>instance_class</c> and <c>name</c>
/// <c>StringName</c>s back out by value — because a scan window over one <c>ClassInfo</c> reaches
/// neighbouring <c>ClassInfo</c>s, and without that last step <c>Control</c>'s window happily yielded
/// <c>Label</c>'s bind (docs/analysis.md §16.1).
/// </para>
/// </remarks>
public sealed record GetterAttribution
{
    /// <summary>The class whose <c>method_map</c> held the bind, as read back off the bind itself.</summary>
    public required string ClassName { get; init; }

    /// <summary>The bound method name, as read back off the bind itself.</summary>
    public required string MethodName { get; init; }

    /// <summary>Address of the <c>MethodBind</c> the name resolved to. Zero when unknown.</summary>
    public ulong MethodBindAddress { get; init; }

    /// <summary>The getter's entry point, recovered from the bind's typed method pointer.</summary>
    public ulong CodeAddress { get; init; }

    /// <summary>
    /// <see cref="CodeAddress"/> relative to the module it was found in, which is the form
    /// docs/analysis.md quotes and the only form comparable between runs.
    /// </summary>
    public ulong CodeRva { get; init; }

    /// <summary>
    /// How the names were confirmed to belong to this bind. Free text, always worth logging.
    /// </summary>
    public string Evidence { get; init; } = string.Empty;

    /// <summary>
    /// <see langword="true"/> only when a class, a method and a code address are all present.
    /// </summary>
    /// <remarks>
    /// A code address with no name is exactly the §13.2 failure, and a name with no code address
    /// names nothing that was decoded. Both are refused here rather than at each call site.
    /// </remarks>
    public bool IsNamed =>
        !string.IsNullOrWhiteSpace(ClassName)
        && !string.IsNullOrWhiteSpace(MethodName)
        && CodeAddress != 0;

    /// <summary><c>Class::method</c>, or a placeholder when the attribution is incomplete.</summary>
    public override string ToString() => IsNamed
        ? string.Format(CultureInfo.InvariantCulture, "{0}::{1}", ClassName, MethodName)
        : "(unattributed getter)";
}
