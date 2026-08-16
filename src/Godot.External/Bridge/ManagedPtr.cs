namespace Godot.External.Bridge;

/// <summary>
/// An address in the <b>managed</b> (CLR) heap — a C# object such as
/// <c>MegaCrit.Sts2.Core.Nodes.NGame</c>.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="NativePtr"/>; see that type for why the distinction is a type and
/// not a naming convention.
/// </para>
/// <para>
/// <b>Different lifetime tier.</b> docs/analysis.md §8.8 puts managed addresses in the
/// <em>snapshot</em> tier and native <c>Node*</c> in the <em>scene epoch</em> tier. A
/// <see cref="ManagedPtr"/> is therefore deliberately <b>not</b> epoch-scoped: this library hands one
/// out and stops caring about it, because validating it belongs to the CLR-side reader (LiveClr),
/// which knows about GC movement and snapshot boundaries. Do not cache one across a snapshot.
/// </para>
/// </remarks>
/// <param name="Address">The raw address. Zero is the null reference.</param>
internal readonly record struct ManagedPtr(ulong Address)
{
    /// <summary>The null managed reference.</summary>
    public static ManagedPtr Null => default;

    /// <summary><see langword="true"/> when this is the null reference.</summary>
    public bool IsNull => Address == 0;

    /// <summary>Offsets this reference by <paramref name="delta"/> bytes to address a field.</summary>
    public ManagedPtr AtOffset(int delta) => new(Address + (ulong)(long)delta);

    /// <inheritdoc/>
    public override string ToString() => $"managed:0x{Address:x}";
}
