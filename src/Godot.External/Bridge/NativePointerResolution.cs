namespace Godot.External.Bridge;

/// <summary>Outcome of reading <c>GodotObject.NativePtr</c> off a managed object.</summary>
internal enum NativePointerStatus
{
    /// <summary>The field was located and held a plausible native pointer.</summary>
    Ok = 0,

    /// <summary>
    /// The resolver could not find the field. Distinct from a failed read: the object is probably not
    /// a <c>GodotObject</c> at all, and reading a guessed offset would produce a usable-looking
    /// address (§4.6).
    /// </summary>
    FieldNotResolved = 1,

    /// <summary>The remote read of the field failed.</summary>
    ReadFailed = 2,

    /// <summary>
    /// The field held null. Legitimate — a <c>GodotObject</c> whose engine object has been freed
    /// reads this way — so callers should treat it as "no node", not as corruption.
    /// </summary>
    NullPointer = 3,

    /// <summary>
    /// The field held a misaligned value. Godot never allocates misaligned objects, so this is a
    /// wrong offset or a torn read.
    /// </summary>
    Misaligned = 4,
}

/// <summary>
/// The result of the managed&#8594;native bridge: a <see cref="NativePtr"/> and the evidence for it.
/// </summary>
/// <remarks>
/// docs/analysis.md §4.6 confirmed this direction live — <c>NGame.Instance</c> &#8594;
/// <c>NativePtr = 0x1a9204c5580</c> &#8594; root node name <c>"Game"</c> — and in the same paragraph
/// records what happens when you hand the wrong address to the native accessors: it resolved to the
/// string <c>"is_visible"</c> rather than failing. Nothing downstream can detect that, so this type
/// reports <em>why</em> rather than returning a bare address.
/// </remarks>
internal sealed record NativePointerResolution
{
    internal NativePointerResolution(
        ManagedPtr managedObject,
        NativePointerStatus status,
        NativePtr nativePointer = default,
        int fieldOffset = -1)
    {
        ManagedObject = managedObject;
        Status = status;
        NativePointer = nativePointer;
        FieldOffset = fieldOffset;
    }

    /// <summary>The managed object the pointer was read from.</summary>
    public ManagedPtr ManagedObject { get; }

    /// <summary>Why the resolution succeeded or failed.</summary>
    public NativePointerStatus Status { get; }

    /// <summary>The engine pointer. Meaningful only when <see cref="IsResolved"/>.</summary>
    public NativePtr NativePointer { get; }

    /// <summary>
    /// Offset the field was read at, or <c>-1</c> when the resolver never produced one. Worth
    /// surfacing: it is the single number that would be wrong if a future runtime changed layout.
    /// </summary>
    public int FieldOffset { get; }

    /// <summary>A usable, non-null, aligned native pointer was obtained.</summary>
    public bool IsResolved => Status == NativePointerStatus.Ok;

    /// <inheritdoc/>
    public override string ToString() => IsResolved
        ? $"{Status}: {ManagedObject} +0x{FieldOffset:x} -> {NativePointer}"
        : $"{Status}: {ManagedObject}";
}
