namespace Godot.External.Calibrator.Calibration;

/// <summary>What the managed side of a .NET cell says about one object.</summary>
public sealed record ManagedObjectInfo(
    string TypeName,
    ulong NativePtr,
    IReadOnlyDictionary<string, object?> Fields);

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
