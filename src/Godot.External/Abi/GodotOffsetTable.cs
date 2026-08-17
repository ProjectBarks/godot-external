namespace Godot.External.Abi;

/// <summary>
/// Byte offsets into Godot's C++ structs for one build. Pure data: no reads, no process, no state.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>fast path and a cross-check, never a hard dependency</b> (docs/analysis.md §7b.2,
/// §12.5). Probe 15 re-derived <c>childListHead</c>, <c>parent</c> and <c>size</c> at connect time
/// from structural and semantic anchors with zero prior knowledge, so the intended flow is:
/// calibrate, then compare against the shipped table and <em>warn loudly on divergence</em>. That
/// turns an engine update from a silent breakage into a startup diagnostic.
/// </para>
/// <para>
/// Being a record, a calibrator can override individual fields with <c>with</c> expressions, or
/// generically via <see cref="With(GodotField, int)"/> when the field is only known at runtime.
/// </para>
/// </remarks>
public sealed record GodotOffsetTable
{
    /// <summary><c>CanvasItem::visible</c>, one byte. Release 4.5.1: <c>0x370</c>.</summary>
    public required int CanvasItemVisible { get; init; }

    /// <summary>
    /// Base of Control's cached global position (x at this offset, y one <c>real_t</c> later).
    /// </summary>
    /// <remarks>
    /// <b>No calibrator is asked to derive this, and none does.</b> It is kept because the §4.6
    /// disassembly recorded it, not because anything reads it — see the warning below on why it must
    /// not be read. A cross-check against this entry therefore compares nothing; that is deliberate
    /// rather than an omission, and it is stated here so the entry does not read as measured.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>DO NOT TRUST THIS FIELD.</b> It is a <em>cached field, not a computed transform</em>.
    /// §4.6 settles it from the disassembly: <c>getGlobalPosition</c> (<c>FUN_180012c70</c>)
    /// performs exactly two float reads and no arithmetic — there is no transform composition
    /// anywhere in it. Live, it returned <c>[0,0]</c> for <c>MainMenuTextButtons</c> and
    /// <c>ContinueButton</c> while both had real on-screen positions (§12.3), because the cache
    /// goes stale for nodes positioned via <c>GlobalPosition</c> writes.
    /// </para>
    /// <para>
    /// The correct global position is obtained by <b>composing local positions up the tree</b>
    /// (what <c>scryObject.ts</c>'s <c>computeGlobalPosition</c> does): sum
    /// <see cref="ControlPosition"/> from the node to the root. Read this field only as a cheap
    /// corroborating hint, and never as the answer.
    /// </para>
    /// </remarks>
    public required int ControlGlobalPosition { get; init; }

    /// <summary>
    /// Base of <c>Control::Data::offset[4]</c>; element <c>i</c> is at
    /// <c>ControlOffsets + i * realSize</c>. Release 4.5.1: <c>0x470</c>.
    /// </summary>
    public required int ControlOffsets { get; init; }

    /// <summary><c>Control::Data::scale</c> (Vector2). Release 4.5.1: <c>0x4a8</c>.</summary>
    public required int ControlScale { get; init; }

    /// <summary><c>Control::Data::pos_cache</c> (Vector2) — the local position. Release 4.5.1: <c>0x4b8</c>.</summary>
    public required int ControlPosition { get; init; }

    /// <summary><c>Control::Data::size_cache</c> (Vector2). Release 4.5.1: <c>0x4c0</c>.</summary>
    public required int ControlSize { get; init; }

    /// <summary><c>Node</c> parent pointer. Release 4.5.1: <c>0x128</c>.</summary>
    public required int NodeParent { get; init; }

    /// <summary>
    /// Head pointer of the intrusive child list. Release 4.5.1: <c>0x148</c>. The value here is the
    /// address of the <em>first link node</em>, not of the first child; see
    /// <see cref="ChildLinkNext"/> / <see cref="ChildLinkPayload"/>.
    /// </summary>
    public required int NodeChildListHead { get; init; }

    /// <summary>
    /// <c>Node</c> name: a pointer to <c>StringName::_Data</c>, not an inline string.
    /// Release 4.5.1: <c>0x1c0</c>.
    /// </summary>
    public required int NodeName { get; init; }

    /// <summary>
    /// Godot's <c>ScriptInstance*</c> on the node. Release 4.5.1: <c>0x68</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is not the managed object,</b> despite scry naming its accessor
    /// <c>getDotNetCoreObject</c>. §12.3b's 22nd check failed here and the failure was the useful
    /// part: the managed object is two further hops away (§4.6, verified live):
    /// </para>
    /// <code>
    /// Node* + NodeScriptInstance      -> ScriptInstance*
    ///        + 0x00                      vtable pointer (inside the game exe's .text)
    ///        + ScriptInstanceOwner       back-reference to the owning Node*  (free self-check)
    ///        + ScriptInstanceGcHandle    GCHandle -> *(handle) == managed C# object
    /// </code>
    /// <para>
    /// The reverse direction is simpler: a managed <c>GodotObject</c> carries the engine pointer in
    /// a field literally named <c>NativePtr</c>. Passing the <em>managed</em> address to the native
    /// wrappers instead yields plausible-looking garbage, so this is a quiet mistake to make.
    /// </para>
    /// </remarks>
    public required int NodeScriptInstance { get; init; }

    /// <summary><c>Label::text</c> — CowData buffer pointer. Release 4.5.1: <c>0x800</c>.</summary>
    public required int LabelText { get; init; }

    /// <summary><c>RichTextLabel::text</c> — CowData buffer pointer. Release 4.5.1: <c>0xa78</c>.</summary>
    public required int RichTextLabelText { get; init; }

    /// <summary>
    /// Offset of the <c>next</c> pointer inside a child-list link node (§4.6: <c>readPtr(cur + 0)</c>).
    /// </summary>
    public int ChildLinkNext { get; init; } = 0x00;

    /// <summary>
    /// Offset of the child <c>Node*</c> payload inside a link node (§4.6: <c>readPtr(cur + 0x18)</c>).
    /// Probe 15 derived the head offset by finding the only pointer <c>p</c> in the node where
    /// <c>*(p + 0x18)</c> was a known child, so this constant is also the calibrator's anchor.
    /// </summary>
    public int ChildLinkPayload { get; init; } = 0x18;

    /// <summary>Offset from <c>StringName::_Data</c> to its character-buffer pointer (§4.6: <c>+8</c>).</summary>
    public int StringNameDataToBuffer { get; init; } = 0x08;

    /// <summary>
    /// How far behind a CowData buffer pointer the element count lives. Godot's CowData stores
    /// <c>[refcount][size]</c> <em>ahead</em> of the data and points at the data, so this is
    /// subtracted, and it equals the target's <c>USize</c> width (8 on x64).
    /// </summary>
    /// <remarks>
    /// Every string read path takes this value <em>from the table</em> rather than assuming 8, so
    /// calibrating it actually changes behaviour. An offset that is calibratable in the API but
    /// ignored at read time would be a trap for the §12.5 calibrator, which diffs derived values
    /// against the shipped ones.
    /// </remarks>
    public int CowDataSizeBackOffset { get; init; } = ByteSourceExtensions.PointerWidth;

    /// <summary>
    /// <c>ScriptInstance</c> back-reference to the owning <c>Node*</c> for a <b>.NET</b> script
    /// instance (§4.6: <c>+0x08</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This one is keyed by the <b>implementing class</b>, not by the build: <c>CSharpInstance</c>
    /// and <c>GDScriptInstance</c> are unrelated C++ classes implementing one interface, so the
    /// owner pointer need not sit at the same place in both — see
    /// <see cref="ScriptInstanceOwnerGdScript"/>. The ABI grid measured <c>0x08</c> wherever the
    /// instance was a C# one and <c>0x10</c> wherever it was a GDScript one, across three passes
    /// with no contradiction (2026-08-17).
    /// </para>
    /// <para>
    /// It is <em>not</em> a per-binding fact, tempting as that reading is from the grid's cell names.
    /// A mono export template runs <c>.gd</c> scripts perfectly well, so one process can hold nodes
    /// of both kinds at once and the correct value differs <b>per node</b>. Choose it by reading the
    /// ScriptInstance's own class — its vtable names it — rather than by asking what the build is.
    /// </para>
    /// </remarks>
    public int ScriptInstanceOwner { get; init; } = 0x08;

    /// <summary>
    /// <c>ScriptInstance</c> back-reference to the owning <c>Node*</c> for a <b>GDScript</b>
    /// instance. Grid-measured <c>0x10</c>.
    /// </summary>
    /// <remarks>
    /// A genuine gap in the §4.6 table rather than a calibration defect: scry only ever read a .NET
    /// target, so the GDScript value had never been observed. A calibrator that derived <c>0x10</c>
    /// on a GDScript cell was disagreeing with a table that had no entry for the case.
    /// </remarks>
    public int ScriptInstanceOwnerGdScript { get; init; } = 0x10;

    /// <summary><c>ScriptInstance</c> slot holding the GCHandle to the managed object (§4.6: <c>+0x20</c>).</summary>
    public int ScriptInstanceGcHandle { get; init; } = 0x20;

    /// <summary>Offset of <paramref name="field"/>, for callers that address the table as data.</summary>
    public int this[GodotField field] => Get(field);

    /// <summary>Offset of <paramref name="field"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The enum value is not a known field.</exception>
    public int Get(GodotField field) => field switch
    {
        GodotField.CanvasItemVisible => CanvasItemVisible,
        GodotField.ControlGlobalPosition => ControlGlobalPosition,
        GodotField.ControlOffsets => ControlOffsets,
        GodotField.ControlScale => ControlScale,
        GodotField.ControlPosition => ControlPosition,
        GodotField.ControlSize => ControlSize,
        GodotField.NodeParent => NodeParent,
        GodotField.NodeChildListHead => NodeChildListHead,
        GodotField.NodeName => NodeName,
        GodotField.NodeScriptInstance => NodeScriptInstance,
        GodotField.LabelText => LabelText,
        GodotField.RichTextLabelText => RichTextLabelText,
        GodotField.ChildLinkNext => ChildLinkNext,
        GodotField.ChildLinkPayload => ChildLinkPayload,
        GodotField.StringNameDataToBuffer => StringNameDataToBuffer,
        GodotField.CowDataSizeBackOffset => CowDataSizeBackOffset,
        GodotField.ScriptInstanceOwner => ScriptInstanceOwner,
        GodotField.ScriptInstanceOwnerGdScript => ScriptInstanceOwnerGdScript,
        GodotField.ScriptInstanceGcHandle => ScriptInstanceGcHandle,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown Godot field."),
    };

    /// <summary>
    /// Returns a copy with <paramref name="field"/> set to <paramref name="offset"/>. This is how a
    /// calibrated value replaces a shipped one without the calibrator knowing property names.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The enum value is not a known field.</exception>
    public GodotOffsetTable With(GodotField field, int offset) => field switch
    {
        GodotField.CanvasItemVisible => this with { CanvasItemVisible = offset },
        GodotField.ControlGlobalPosition => this with { ControlGlobalPosition = offset },
        GodotField.ControlOffsets => this with { ControlOffsets = offset },
        GodotField.ControlScale => this with { ControlScale = offset },
        GodotField.ControlPosition => this with { ControlPosition = offset },
        GodotField.ControlSize => this with { ControlSize = offset },
        GodotField.NodeParent => this with { NodeParent = offset },
        GodotField.NodeChildListHead => this with { NodeChildListHead = offset },
        GodotField.NodeName => this with { NodeName = offset },
        GodotField.NodeScriptInstance => this with { NodeScriptInstance = offset },
        GodotField.LabelText => this with { LabelText = offset },
        GodotField.RichTextLabelText => this with { RichTextLabelText = offset },
        GodotField.ChildLinkNext => this with { ChildLinkNext = offset },
        GodotField.ChildLinkPayload => this with { ChildLinkPayload = offset },
        GodotField.StringNameDataToBuffer => this with { StringNameDataToBuffer = offset },
        GodotField.CowDataSizeBackOffset => this with { CowDataSizeBackOffset = offset },
        GodotField.ScriptInstanceOwner => this with { ScriptInstanceOwner = offset },
        GodotField.ScriptInstanceOwnerGdScript => this with { ScriptInstanceOwnerGdScript = offset },
        GodotField.ScriptInstanceGcHandle => this with { ScriptInstanceGcHandle = offset },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown Godot field."),
    };
}
