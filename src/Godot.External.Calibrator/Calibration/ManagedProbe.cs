namespace Godot.External.Calibrator.Calibration;

/// <summary>What the managed side of a .NET cell says about one object.</summary>
/// <param name="TypeName">Managed type the object resolved to.</param>
/// <param name="NativePtr">Value of the object's <c>NativePtr</c> field.</param>
/// <param name="Fields">Requested fields that were read, keyed by name.</param>
/// <param name="FieldRefusals">
/// One sentence per requested field that was <em>not</em> read, saying why.
/// </param>
/// <remarks>
/// A field that is absent from <paramref name="Fields"/> and unmentioned in
/// <paramref name="FieldRefusals"/> would be the §4.6 failure this record exists to prevent: the
/// bridge resolved an object, read nothing off it, and said nothing about that. Silence and a
/// considered refusal are indistinguishable from outside, so the refusals travel with the values.
/// </remarks>
public sealed record ManagedObjectInfo(
    string TypeName,
    ulong NativePtr,
    IReadOnlyDictionary<string, object?> Fields,
    IReadOnlyList<string>? FieldRefusals = null);

/// <summary>
/// The managed half of the §4.6 bridge, behind an interface so the derivation can be tested without
/// a CLR.
/// </summary>
public interface IManagedProbe
{
    /// <summary>
    /// Describes the managed object at <paramref name="address"/>: its type, the value of its
    /// <c>NativePtr</c> field, and any of <paramref name="fieldNames"/> it carries.
    /// </summary>
    bool TryDescribe(ulong address, IReadOnlyList<string> fieldNames, out ManagedObjectInfo info);
}
