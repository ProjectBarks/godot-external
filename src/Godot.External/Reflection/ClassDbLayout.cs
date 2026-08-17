namespace Godot.External.Reflection;

/// <summary>
/// Structure offsets the <c>ClassDB</c> walk needs, per engine version.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mixed provenance, and the difference matters.</b> <see cref="ElementNext"/>,
/// <see cref="ElementPrevious"/> and <see cref="ElementData"/> come straight from
/// <c>core/templates/hash_map.h</c>, where <c>HashMapElement</c> declares
/// <c>next</c>, <c>prev</c>, then <c>KeyValue data</c> in that order with no virtuals — so those
/// three are as solid as reading the source. <see cref="HashMapSize"/> and
/// <see cref="StringNameHasCompileTimeName"/> record differences between 4.3 and 4.5 that are
/// documented but that <b>this module has never confirmed against a live process</b>.
/// </para>
/// <para>
/// Nothing here may reach a <c>GodotAbiProfile</c>. See <see cref="ClassDbElementWalk"/> for why the
/// whole Phase 2 chain is quarantined.
/// </para>
/// </remarks>
internal sealed record ClassDbLayout
{
    /// <summary>Godot 4.5: 40-byte <c>HashMap</c>, <c>StringName::_Data</c> without <c>cname</c>/<c>idx</c>.</summary>
    public static ClassDbLayout Godot45 { get; } = new()
    {
        HashMapSize = 40,
        StringNameHasCompileTimeName = false,
    };

    /// <summary>Godot 4.3: 48-byte <c>HashMap</c>, <c>StringName::_Data</c> still carrying <c>cname</c>.</summary>
    public static ClassDbLayout Godot43 { get; } = new()
    {
        HashMapSize = 48,
        StringNameHasCompileTimeName = true,
    };

    /// <summary><c>HashMapElement::next</c>. From <c>hash_map.h</c>: first member, no vtable.</summary>
    public int ElementNext { get; init; }

    /// <summary><c>HashMapElement::prev</c>, immediately after <c>next</c>.</summary>
    public int ElementPrevious { get; init; } = 8;

    /// <summary><c>HashMapElement::data</c> — the <c>KeyValue</c>, whose key is its first member.</summary>
    public int ElementData { get; init; } = 16;

    /// <summary>
    /// <c>sizeof(HashMap)</c> for this version — 48 in 4.3, 40 in 4.5. Only needed by a walker that
    /// strides across maps; the element chain does not use it.
    /// </summary>
    public int HashMapSize { get; init; }

    /// <summary>
    /// Whether <c>StringName::_Data</c> still begins with the <c>cname</c> compile-time pointer
    /// (true through 4.3, removed in 4.5). A 4.3 reader must test <c>cname</c> <em>before</em> the
    /// heap <c>name</c>, or it reads an empty string for every statically named class.
    /// </summary>
    public bool StringNameHasCompileTimeName { get; init; }
}
