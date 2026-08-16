namespace Godot.External.Abi;

/// <summary>
/// Every offset a profile carries, addressable as data. The calibrator (docs/analysis.md §12.5)
/// needs to write offsets it has derived without knowing their names at compile time, so the table
/// is indexable by this enum as well as by property.
/// </summary>
public enum GodotField
{
    /// <summary><c>CanvasItem::visible</c> — one byte.</summary>
    CanvasItemVisible,

    /// <summary>
    /// Control's <b>cached</b> global position (x, then y one <c>real_t</c> later).
    /// <b>Frequently stale.</b> See <see cref="GodotOffsetTable.ControlGlobalPosition"/>.
    /// </summary>
    ControlGlobalPosition,

    /// <summary><c>Control::Data::offset[4]</c> — base of four consecutive <c>real_t</c>s.</summary>
    ControlOffsets,

    /// <summary><c>Control::Data::scale</c> — Vector2.</summary>
    ControlScale,

    /// <summary><c>Control::Data::pos_cache</c> — Vector2, what <c>getPosition</c> returns.</summary>
    ControlPosition,

    /// <summary><c>Control::Data::size_cache</c> — Vector2, what <c>getSize</c> returns.</summary>
    ControlSize,

    /// <summary><c>Node</c>'s parent pointer.</summary>
    NodeParent,

    /// <summary>Head pointer of <c>Node</c>'s intrusive child list.</summary>
    NodeChildListHead,

    /// <summary><c>Node</c>'s name — a <c>StringName</c> handle, not an inline string.</summary>
    NodeName,

    /// <summary>
    /// <c>Node</c>'s <c>ScriptInstance*</c>. Named <c>getDotNetCoreObject</c> in scry, but it is
    /// <b>not</b> the managed object — see <see cref="GodotOffsetTable.NodeScriptInstance"/>.
    /// </summary>
    NodeScriptInstance,

    /// <summary><c>Label::text</c> — a Godot <c>String</c> (CowData buffer pointer).</summary>
    LabelText,

    /// <summary><c>RichTextLabel::text</c> — a Godot <c>String</c>, at a different offset than Label's.</summary>
    RichTextLabelText,

    /// <summary>Link-node member holding the next link (§4.6: <c>next = readPtr(cur + 0)</c>).</summary>
    ChildLinkNext,

    /// <summary>Link-node member holding the child <c>Node*</c> (§4.6: <c>readPtr(cur + 0x18)</c>).</summary>
    ChildLinkPayload,

    /// <summary><c>StringName::_Data</c> to its character buffer pointer.</summary>
    StringNameDataToBuffer,

    /// <summary>How far <em>behind</em> a CowData buffer pointer its element count sits.</summary>
    CowDataSizeBackOffset,

    /// <summary><c>ScriptInstance</c> back-reference to its owning <c>Node*</c> — a free self-check.</summary>
    ScriptInstanceOwner,

    /// <summary><c>ScriptInstance</c> slot holding the GCHandle to the managed C# object.</summary>
    ScriptInstanceGcHandle,
}
