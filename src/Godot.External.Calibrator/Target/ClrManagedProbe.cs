using Godot.External.Calibrator.Calibration;
using LiveClr;
using LiveClr.Runtime;

namespace Godot.External.Calibrator.Target;

/// <summary>
/// The managed half of §4.6's bridge, over LiveClr's cDAC-based object model.
/// </summary>
/// <remarks>
/// <para>
/// Direction matters here. The obvious route is managed static → <c>NativePtr</c>, but LiveClr
/// deliberately does not resolve static field <em>addresses</em> (the .NET 9 contract descriptor
/// publishes no <c>DomainLocalModule</c> and no <c>FieldDesc</c>, so there is no descriptor-only
/// path from a type name to a static's slot). This probe therefore starts from a managed object
/// address the <em>native</em> derivation already produced — node → <c>ScriptInstance</c> →
/// <c>GCHandle</c> — and goes up from there, which is also §5.5's sanctioned bootstrap: resolving one
/// object registers its module, after which types resolve by name.
/// </para>
/// <para>
/// The bridge is then verified in the direction that matters: the managed object's own
/// <c>NativePtr</c> field must lead back to the node the walk calls the root. §4.6 records that
/// handing the <em>managed</em> address to the native wrappers instead yields plausible-looking
/// garbage — it once resolved to the string <c>"is_visible"</c> — so a near miss is the expected
/// shape of this bug rather than an obvious one.
/// </para>
/// </remarks>
public sealed class ClrManagedProbe : IDisposable, IManagedProbe
{
    /// <summary>The field a managed <c>GodotObject</c> carries its engine pointer in (§4.6).</summary>
    public const string NativePointerField = "NativePtr";

    private readonly LiveProcess _process;
    private ISnapshot? _snapshot;

    private ClrManagedProbe(LiveProcess process) => _process = process;

    /// <summary>Attaches, or explains in <paramref name="notes"/> why the managed side is unavailable.</summary>
    public static ClrManagedProbe? TryAttach(int processId, ICollection<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        try
        {
            if (!LiveProcess.TryAttach(processId, out LiveProcess? process, out string? failure))
            {
                notes.Add($"managed probe: could not attach to the CLR in pid {processId}: {failure}");
                return null;
            }

            if (!process.FieldDescCalibration.IsCalibrated)
            {
                notes.Add("managed probe: attached, but instance field offsets did not calibrate "
                        + $"({process.FieldDescCalibration.Detail}); managed fields will not be read.");
            }

            return new ClrManagedProbe(process);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            notes.Add($"managed probe: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public bool TryDescribe(ulong address, IReadOnlyList<string> fieldNames, out ManagedObjectInfo info)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);

        info = new ManagedObjectInfo(string.Empty, 0, new Dictionary<string, object?>());

        try
        {
            _snapshot ??= _process.BeginSnapshot();
            IClrObject? managed = _snapshot.Object(address);
            if (managed is null)
            {
                return false;
            }

            IClrValue? nativePtr = managed.Field(NativePointerField);
            if (nativePtr is null || nativePtr.IsNull)
            {
                return false;
            }

            Dictionary<string, object?> fields = [];
            foreach (string name in fieldNames)
            {
                // Only string fields are published. Nothing in the request describes the declared
                // type of a managed field, and reading an int as a long (or the other way) would
                // produce a plausible wrong number rather than a failure.
                string? value = managed.Field(name)?.AsString();
                if (value is not null)
                {
                    fields[name] = value;
                }
            }

            info = new ManagedObjectInfo(managed.Type.Name, nativePtr.Read<ulong>(), fields);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _snapshot?.Dispose();
        _process.Dispose();
    }
}
