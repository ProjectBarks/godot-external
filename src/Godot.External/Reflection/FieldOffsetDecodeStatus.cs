namespace Godot.External.Reflection;

/// <summary>
/// Why <see cref="GetterFieldDecoder"/> did or did not publish an offset.
/// </summary>
/// <remarks>
/// <para>
/// Every value except <see cref="Decoded"/> is a refusal. docs/analysis.md §8.9 and
/// <c>AbsentNeverWrongTests</c> set the governing property for this codebase: <b>where it is unsure,
/// it says nothing</b>. A getter body is a very small sample of a very large instruction set, so the
/// decoder is built to fail closed — the interesting engineering here is the refusal set, not the
/// success path.
/// </para>
/// <para>
/// The distinctions are kept fine-grained because they diagnose different things.
/// <see cref="NoThisRelativeAccess"/> means "this is not a field getter" (computed, delegating, or a
/// constant), which the source census puts at ~15.6% of <c>ADD_PROPERTY</c> sites and which
/// <em>should</em> refuse. <see cref="AmbiguousAccesses"/> means "this reads more than one field and
/// the decoder cannot tell which one the property is" — a genuine near-miss worth logging.
/// <see cref="UndecodableBody"/> means the bytes are not code at all, which normally means the
/// caller handed over a wrong address and no offset from that address should be trusted.
/// </para>
/// </remarks>
public enum FieldOffsetDecodeStatus
{
    /// <summary>Exactly one <c>this</c>-relative field access was found. The offset is published.</summary>
    Decoded = 0,

    /// <summary>
    /// The buffer was empty, or shorter than the smallest getter that could be proved
    /// (<c>movzx eax, byte [rcx+disp32]; ret</c> is 8 bytes).
    /// </summary>
    EmptyBody,

    /// <summary>
    /// The bytes stopped decoding as valid x86-64 before a <c>ret</c> was reached. Almost always a
    /// bad function address rather than exotic codegen.
    /// </summary>
    UndecodableBody,

    /// <summary>
    /// No <c>ret</c> within the inspection window. Trivial getters are single-digit instructions;
    /// anything that runs past the window is a real function body whose relationship between its
    /// loads and its return value the decoder cannot establish.
    /// </summary>
    NoReturnInWindow,

    /// <summary>
    /// The straight-line prefix touched no memory through the <c>this</c> register at an accepted
    /// displacement. This is the correct answer for computed getters (<c>Engine::get_time_scale</c>),
    /// predicate getters (<c>InputEvent::is_pressed</c>), and getters returning a constant.
    /// </summary>
    NoThisRelativeAccess,

    /// <summary>
    /// Two or more distinct <c>this</c>-relative fields were touched before the first call or return,
    /// so no single one can be attributed to the property. <c>CanvasItem::is_visible_in_tree</c> —
    /// which reads <c>visible</c> at D and <c>parent_visible_in_tree</c> at D+1 — lands here, and
    /// must: it is a different method than <c>is_visible</c> even though it shares a field.
    /// </summary>
    AmbiguousAccesses,

    /// <summary>
    /// A displacement was found but fell outside the accepted band — below
    /// <see cref="GetterDecoderOptions.MinimumDisplacement"/> (vtable/refcount/header territory) or
    /// above <see cref="GetterDecoderOptions.MaximumDisplacement"/> (not a field of any Godot class).
    /// Distinguished from <see cref="NoThisRelativeAccess"/> because it means "found something and
    /// rejected it", which is a different diagnosis.
    /// </summary>
    DisplacementOutOfRange,

    /// <summary>
    /// A single unambiguous field access was found, but the body called out before returning and the
    /// caller set <see cref="GetterDecoderOptions.RequireLeafBody"/>. Opt-in strictness, not a defect
    /// in the body — see <see cref="GetterShape.HelperCall"/>.
    /// </summary>
    LeafBodyRequired,

    /// <summary>
    /// The requested <see cref="NativeCallingConvention"/> is not <see cref="NativeCallingConvention.MsvcX64"/>.
    /// See the remarks on that enum: the getter-code route is Windows-only by construction.
    /// </summary>
    UnsupportedCallingConvention,
}
