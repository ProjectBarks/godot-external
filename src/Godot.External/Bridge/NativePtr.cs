namespace Godot.External.Bridge;

/// <summary>
/// An address in the <b>native</b> Godot engine heap — a <c>Node*</c>, a <c>ScriptInstance*</c>, a
/// <c>CowData</c> buffer.
/// </summary>
/// <remarks>
/// <para>
/// This type exists to make one specific documented mistake impossible to make silently.
/// docs/analysis.md §4.6: a managed <c>GodotObject</c> holds the engine pointer in a field named
/// <c>NativePtr</c>, and "passing the <em>managed</em> address instead yields plausible-looking
/// garbage (it resolved to the string <c>is_visible</c>)". Both addresses are 64-bit integers in the
/// same process, so <see cref="ulong"/> cannot tell them apart and neither can a reviewer.
/// <see cref="NativePtr"/> and <see cref="ManagedPtr"/> can, at compile time.
/// </para>
/// <para>
/// A <see cref="NativePtr"/> is <b>inert</b>: it has no read methods. Reading requires a live
/// <c>SceneEpoch</c>, because §8.8 makes native pointers epoch-tier state — Godot frees nodes and
/// reuses the allocation, so an address on its own is not evidence that anything is there.
/// </para>
/// </remarks>
/// <param name="Address">The raw address. Zero is the null pointer.</param>
internal readonly record struct NativePtr(ulong Address)
{
    /// <summary>The null native pointer — a legitimate value (a root node has no parent).</summary>
    public static NativePtr Null => default;

    /// <summary><see langword="true"/> when this is the null pointer.</summary>
    public bool IsNull => Address == 0;

    /// <summary>
    /// <see langword="true"/> when the address is aligned to <paramref name="pointerSize"/>. Godot's
    /// allocator never hands out misaligned objects, so a misaligned value is a torn or fabricated
    /// read rather than a node (the check <c>ChildListWalk</c> already applies to link nodes).
    /// </summary>
    public bool IsAligned(int pointerSize) => pointerSize > 0 && (Address & (ulong)(pointerSize - 1)) == 0;

    /// <summary>Offsets this pointer by <paramref name="delta"/> bytes to address a field.</summary>
    public NativePtr AtOffset(int delta) => new(Address + (ulong)(long)delta);

    /// <inheritdoc/>
    public override string ToString() => $"native:0x{Address:x}";
}
