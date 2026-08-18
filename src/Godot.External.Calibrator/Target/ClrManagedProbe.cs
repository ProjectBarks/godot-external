using Godot.External.Calibrator.Calibration;
using LiveClr;
using LiveClr.Memory;
using LiveClr.Runtime;
using LiveClr.Snapshots;

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
    private readonly ICollection<string> _notes;
    private ISnapshot? _snapshot;
    private bool _reportedStringGate;

    private ClrManagedProbe(LiveProcess process, ICollection<string> notes)
    {
        _process = process;
        _notes = notes;
    }

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

            return new ClrManagedProbe(process, notes);
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
            List<string> refusals = [];
            foreach (string name in fieldNames)
            {
                IClrValue? slot = managed.Field(name);
                if (slot is null)
                {
                    refusals.Add($"{name}: no instance field of that name is declared on {managed.Type.Name} or "
                               + "any of its ancestors, or its offset did not resolve");
                    continue;
                }

                if (TryReadDeclared(_snapshot, slot, out object? value, out string refusal))
                {
                    fields[name] = value;
                }
                else
                {
                    refusals.Add($"{name}: {refusal}");
                }
            }

            info = new ManagedObjectInfo(managed.Type.Name, nativePtr.Read<ulong>(), fields, refusals);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads one field slot at the width its own metadata declares, or refuses and says why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The declared type decides the width; nothing here guesses one.</b> The request names six
    /// fields and says nothing about their types, and the two failure modes that follow from that
    /// are not hypothetical: reading <c>ProbeInt32</c> as eight bytes picks up whatever the next
    /// field happens to be and reports a plausible number, and a wrong reference offset can still
    /// decode into a perfectly valid string. Neither shows up as an error. So the only offset used
    /// is the runtime's own <c>FieldDesc</c> offset, and the only width used is the one ECMA-335
    /// signature decoding produced — <see cref="ClrValue.Shape"/>.
    /// </para>
    /// <para>
    /// An element type this method does not enumerate is refused rather than read as bytes.
    /// A slot whose signature decoded to <c>Unknown</c>, or to a generic parameter, is exactly the
    /// case where a default width would be invention.
    /// </para>
    /// </remarks>
    private bool TryReadDeclared(ISnapshot snapshot, IClrValue slot, out object? value, out string refusal)
    {
        value = null;

        if (slot is not ClrValue typed)
        {
            refusal = "the field handle did not carry a declared shape, so the width to read is unknown";
            return false;
        }

        ClrElementType declared = typed.Shape.ElementType;
        string named = typed.Shape.TypeName ?? declared.ToString();

        if (declared is ClrElementType.String)
        {
            return TryReadString(snapshot, typed, out value, out refusal);
        }

        bool read;
        switch (declared)
        {
            case ClrElementType.Boolean: read = typed.TryRead(out byte flag); value = flag != 0; break;
            case ClrElementType.SByte: read = typed.TryRead(out sbyte i8); value = i8; break;
            case ClrElementType.Byte: read = typed.TryRead(out byte u8); value = u8; break;
            case ClrElementType.Int16: read = typed.TryRead(out short i16); value = i16; break;
            case ClrElementType.UInt16: read = typed.TryRead(out ushort u16); value = u16; break;
            case ClrElementType.Char: read = typed.TryRead(out char ch); value = ch.ToString(); break;
            case ClrElementType.Int32: read = typed.TryRead(out int i32); value = i32; break;
            case ClrElementType.UInt32: read = typed.TryRead(out uint u32); value = u32; break;
            case ClrElementType.Int64: read = typed.TryRead(out long i64); value = i64; break;
            case ClrElementType.UInt64: read = typed.TryRead(out ulong u64); value = u64; break;
            case ClrElementType.Single: read = typed.TryRead(out float f32); value = f32; break;
            case ClrElementType.Double: read = typed.TryRead(out double f64); value = f64; break;

            default:
                refusal = $"declared type is {named} ({declared}), which this probe does not read; "
                        + "the alternative is choosing a width the metadata did not state";
                return false;
        }

        if (!read)
        {
            value = null;
            refusal = $"declared {named}, but the slot at 0x{typed.Address:x} could not be read";
            return false;
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// Decodes a <c>System.String</c> field, going around <see cref="IClrValue.AsString"/> when that
    /// refuses a string the runtime does not label as one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not simply <c>slot.AsString()</c>.</b> That method gates on
    /// <c>ClrTypeInfo.IsString</c>, which is <c>EEClass.InternalCorElementType ==
    /// ELEMENT_TYPE_STRING</c>. Measured on a live .NET 9 Godot target, <c>System.String</c>'s
    /// <c>EEClass</c> reports <c>ELEMENT_TYPE_CLASS</c> (0x12) instead — CoreCLR carries "this is a
    /// string" in the <em>MethodTable</em> category flags, not in the <c>EEClass</c> norm type — so
    /// <c>AsString</c> returned null for every string field on every cell, and both string values
    /// went unread. The gate belongs to LiveClr and the fix belongs there; until it lands, this
    /// decodes the string itself and <em>says so in the notes</em> rather than quietly succeeding —
    /// a workaround nobody can see is how a known gap becomes an unknown one.
    /// </para>
    /// <para>
    /// <b>Nothing is swept and nothing is hardcoded.</b> The slot offset is the runtime's own
    /// <c>FieldDesc</c> offset, the header offsets are the descriptor's own
    /// <c>String.m_StringLength</c> / <c>String.m_FirstChar</c> (§5.2), and the decode only runs
    /// once the pointed-at object's method table resolves, through metadata, to a type NAMED
    /// <c>System.String</c>. That last gate is what keeps this honest: §4.6's expensive failure was
    /// a wrong offset decoding into a perfectly valid string, and a decode with no candidates to
    /// choose between cannot pick the wrong one.
    /// </para>
    /// </remarks>
    private bool TryReadString(ISnapshot snapshot, ClrValue slot, out object? value, out string refusal)
    {
        value = slot.AsString();
        if (value is not null)
        {
            refusal = string.Empty;
            return true;
        }

        ulong reference = slot.ReadPointer();
        if (slot.AsObject() is not IClrObject target)
        {
            refusal = $"declared System.String, but the reference 0x{reference:x} in that slot did not "
                    + "validate as a managed object";
            return false;
        }

        if (!string.Equals(target.Type.Name, StringTypeName, StringComparison.Ordinal))
        {
            refusal = $"declared System.String, but the object at 0x{reference:x} is a "
                    + $"\"{target.Type.Name}\"";
            return false;
        }

        if (snapshot is not LiveSnapshot live)
        {
            refusal = $"the string at 0x{reference:x} needs the descriptor's String header offsets, which "
                    + $"a {snapshot.GetType().Name} does not expose";
            return false;
        }

        int lengthOffset = live.TypeSystem.Layouts.StringLengthOffset;
        int firstCharOffset = live.TypeSystem.Layouts.StringFirstCharOffset;

        if (!live.Memory.TryRead(target.Address + (ulong)lengthOffset, out int length))
        {
            refusal = $"the String at 0x{reference:x} did not yield a readable m_StringLength";
            return false;
        }

        if (length < 0 || length > MaxStringLength)
        {
            // A length is a DECODED number, and a bad decode produces an enormous one rather than an
            // obviously invalid one. Refusing is the only safe response; sizing a read from it is not.
            refusal = $"the String at 0x{reference:x} reports an implausible length of {length}";
            return false;
        }

        byte[] utf16 = new byte[length * 2];
        if (!live.Memory.TryRead(target.Address + (ulong)firstCharOffset, utf16))
        {
            refusal = $"the String at 0x{reference:x} claims {length} char(s) but its character data "
                    + "could not be read";
            return false;
        }

        value = System.Text.Encoding.Unicode.GetString(utf16);

        if (!_reportedStringGate)
        {
            _reportedStringGate = true;
            _notes.Add(
                "managed probe: IClrValue.AsString() refused a genuine System.String on this target — "
                + $"its EEClass.InternalCorElementType reads {(slot.AsObject() as ClrObject)?.TypeInfo.ElementType} "
                + "rather than String, which is where CoreCLR puts the class category rather than the "
                + "string marker. The string was decoded here from the descriptor's own "
                + $"String.m_StringLength (+0x{lengthOffset:x}) and String.m_FirstChar (+0x{firstCharOffset:x}) "
                + "instead, gated on the object's metadata type name. This is a LiveClr gap worked around "
                + "in the calibrator, not a fix.");
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Metadata name the pointed-at object must carry before its bytes are decoded as text.</summary>
    private const string StringTypeName = "System.String";

    /// <summary>Longer than any value this probe reads; past this a length is a bad decode, not data.</summary>
    private const int MaxStringLength = 1 << 22;

    /// <inheritdoc/>
    public void Dispose()
    {
        _snapshot?.Dispose();
        _process.Dispose();
    }
}
