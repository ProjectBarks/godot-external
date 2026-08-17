using System.Globalization;

namespace Godot.External.Reflection;

/// <summary>
/// The outcome of decoding one property getter's machine code: either a field offset, or a named
/// reason no offset was published.
/// </summary>
/// <remarks>
/// <para>
/// Returned rather than thrown, matching <c>ChildWalkResult</c> and docs/analysis.md §8.8: a refusal
/// is an ordinary, expected outcome here — roughly one property getter in six is computed or
/// delegating and <em>must</em> refuse — so it cannot be an exception.
/// </para>
/// <para>
/// <see cref="Offset"/> is deliberately nullable and only ever non-null when
/// <see cref="Status"/> is <see cref="FieldOffsetDecodeStatus.Decoded"/>. There is no "offset plus a
/// flag saying ignore it" state to get wrong at a call site.
/// </para>
/// </remarks>
public sealed class FieldOffsetDecodeResult
{
    private FieldOffsetDecodeResult(
        FieldOffsetDecodeStatus status,
        string reason,
        int? offset,
        GetterShape shape,
        int accessSize,
        bool isFloatingPointAccess,
        bool usedHiddenReturnPointer)
    {
        Status = status;
        Reason = reason;
        Offset = offset;
        Shape = shape;
        AccessSize = accessSize;
        IsFloatingPointAccess = isFloatingPointAccess;
        UsedHiddenReturnPointer = usedHiddenReturnPointer;
    }

    /// <summary>Whether an offset was published, and if not, which refusal applied.</summary>
    public FieldOffsetDecodeStatus Status { get; }

    /// <summary>
    /// Human-readable diagnosis, always populated. Carries the detail the enum cannot — which
    /// displacements collided, how far the body ran, what the offending instruction was.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// The decoded field offset, or <see langword="null"/> for every refusal.
    /// </summary>
    public int? Offset { get; }

    /// <summary>How completely the body was accounted for. Only meaningful when decoded.</summary>
    public GetterShape Shape { get; }

    /// <summary>
    /// Width in bytes of the first access to the field (1 for a <c>bool</c>, 4 for <c>int32</c> or
    /// <c>float</c>, 8 for a pointer or <c>String</c> handle). Zero when refused.
    /// </summary>
    /// <remarks>
    /// This is a cheap type cross-check: a caller asking for <c>Label::text</c> — a
    /// <c>CowData</c> buffer pointer — and getting <see cref="AccessSize"/> 1 has followed the wrong
    /// <c>MethodBind</c>, regardless of how plausible the offset looks.
    /// </remarks>
    public int AccessSize { get; }

    /// <summary>
    /// <see langword="true"/> when the field was read into an XMM register (<c>movss</c>/<c>movsd</c>),
    /// i.e. the property is a <c>float</c>/<c>double</c> rather than an integer or pointer.
    /// </summary>
    public bool IsFloatingPointAccess { get; }

    /// <summary>
    /// <see langword="true"/> when <c>this</c> was found in RDX rather than RCX — the MSVC shape for
    /// a return value too large for RAX, where RCX holds the caller's hidden result buffer.
    /// </summary>
    /// <remarks>
    /// Also a cross-check: <c>String</c>, <c>Ref&lt;T&gt;</c>, <c>Transform2D</c> and friends must be
    /// <see langword="true"/>; a <c>bool</c> or <c>int</c> property must be <see langword="false"/>.
    /// A mismatch against the property's declared type means the address was wrong.
    /// </remarks>
    public bool UsedHiddenReturnPointer { get; }

    /// <summary><see langword="true"/> only when an offset is present.</summary>
    public bool IsDecoded => Status == FieldOffsetDecodeStatus.Decoded;

    internal static FieldOffsetDecodeResult Success(
        int offset,
        GetterShape shape,
        int accessSize,
        bool isFloatingPointAccess,
        bool usedHiddenReturnPointer)
    {
        string register = usedHiddenReturnPointer ? "rdx" : "rcx";
        string reason = string.Format(
            CultureInfo.InvariantCulture,
            "single {0}-relative {1}-byte {2} access at +0x{3:x} ({4})",
            register,
            accessSize,
            isFloatingPointAccess ? "float" : "integer/pointer",
            offset,
            shape);

        return new FieldOffsetDecodeResult(
            FieldOffsetDecodeStatus.Decoded,
            reason,
            offset,
            shape,
            accessSize,
            isFloatingPointAccess,
            usedHiddenReturnPointer);
    }

    internal static FieldOffsetDecodeResult Refuse(FieldOffsetDecodeStatus status, string reason)
        => new(status, reason, offset: null, GetterShape.LeafLoad, accessSize: 0, false, false);

    /// <inheritdoc/>
    public override string ToString() => IsDecoded
        ? string.Format(CultureInfo.InvariantCulture, "Decoded +0x{0:x} — {1}", Offset, Reason)
        : string.Format(CultureInfo.InvariantCulture, "{0} — {1}", Status, Reason);
}
