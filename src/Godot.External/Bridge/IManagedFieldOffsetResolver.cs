namespace Godot.External.Bridge;

/// <summary>
/// Resolves the byte offset of an instance field on a managed object. This is the whole of this
/// library's dependency on the CLR-side reader.
/// </summary>
/// <remarks>
/// <para>
/// The managed&#8594;native direction needs exactly one thing that this repo cannot compute: where the
/// <c>NativePtr</c> field sits on a <c>GodotObject</c>. Answering that means reading the object's
/// <c>MethodTable</c>, its <c>Module</c>, and the ECMA-335 metadata — proven doable with no ClrMD and
/// no DAC in docs/analysis.md §12.4d, and slated to live in LiveClr (§8.8), which this repo does not
/// reference yet.
/// </para>
/// <para>
/// So it is an interface, not an import. A LiveClr adapter implements it in a few lines; tests
/// implement it with a dictionary; and §8.8's hard name boundary is preserved because nothing here
/// knows a type name.
/// </para>
/// <para>
/// The <em>address</em> is passed rather than a type handle because a field's offset depends on the
/// object's concrete type, which only the resolver can determine — and because the resolver may want
/// to cache per <c>MethodTable</c> (§12.4, API fact 1: field enumeration is the single biggest cost
/// in a naive traversal, and is cacheable per class).
/// </para>
/// </remarks>
internal interface IManagedFieldOffsetResolver
{
    /// <summary>
    /// Resolves <paramref name="fieldName"/> on the object at <paramref name="managedObject"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the type is unknown or has no such field. Returning
    /// <see langword="false"/> is strongly preferred to guessing: a wrong offset produces a
    /// plausible-looking pointer, which §4.6 records as an easy and quiet mistake.
    /// </returns>
    bool TryGetInstanceFieldOffset(ManagedPtr managedObject, string fieldName, out int offset);
}

/// <summary>
/// Adapts a lambda to <see cref="IManagedFieldOffsetResolver"/>, so a caller that already has a
/// field-offset function does not have to declare a class.
/// </summary>
/// <param name="resolve">
/// Returns the field's offset, or <see langword="null"/> when it cannot be resolved.
/// </param>
internal sealed class DelegateManagedFieldOffsetResolver(Func<ManagedPtr, string, int?> resolve)
    : IManagedFieldOffsetResolver
{
    private readonly Func<ManagedPtr, string, int?> _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));

    /// <inheritdoc/>
    public bool TryGetInstanceFieldOffset(ManagedPtr managedObject, string fieldName, out int offset)
    {
        int? resolved = _resolve(managedObject, fieldName);
        offset = resolved ?? 0;
        return resolved.HasValue;
    }
}
