namespace Godot.External.Reflection;

/// <summary>
/// How much of the getter's body the decoder actually accounted for. A caller that wants the
/// strongest possible evidence can require <see cref="LeafLoad"/>.
/// </summary>
/// <remarks>
/// <para>
/// Both shapes were observed decoding correctly against the shipped 4.5-stable Windows templates, so
/// neither is rejected outright — but they are not equally strong evidence, and collapsing them
/// would hide that.
/// </para>
/// <para>
/// <b><see cref="LeafLoad"/></b> is a closed proof: every instruction between entry and <c>ret</c>
/// was inspected, exactly one field was touched, and nothing else could have influenced the return
/// value. <c>CanvasItem::is_visible</c> (<c>movzx eax, byte [rcx+0x370]; ret</c>) is this.
/// </para>
/// <para>
/// <b><see cref="HelperCall"/></b> is an inference: the field address was formed, then control left
/// for a callee the decoder did not follow. Godot's <c>String</c>/<c>Ref&lt;T&gt;</c> getters are all
/// this shape because returning a copy-on-write value calls into a reference-count helper — the 4.5
/// release <c>get_text</c> at RVA <c>0x1483bb0</c> loads <c>[rdx+0x800]</c> and then calls
/// <c>CowData::_ref</c>. The offset is still right, but the argument for it is "nothing else
/// <c>this</c>-relative was touched first", not "nothing else happened".
/// </para>
/// </remarks>
public enum GetterShape
{
    /// <summary>
    /// The body reached its <c>ret</c> without leaving the function. Strongest form.
    /// </summary>
    LeafLoad = 0,

    /// <summary>
    /// A <c>call</c> or tail <c>jmp</c> intervened after the field access. Accepted, but a
    /// cross-check should weigh it below <see cref="LeafLoad"/>.
    /// </summary>
    HelperCall = 1,
}
