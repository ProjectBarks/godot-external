namespace Godot.External.Bridge;

/// <summary>
/// Every hop of the native&#8594;managed bridge, plus how far it got. Returned rather than thrown, per
/// docs/analysis.md §8.8's error model.
/// </summary>
/// <remarks>
/// The intermediate pointers are exposed on purpose. §12.3b found the original single-hop model
/// wrong only because the intermediate values were printed and compared against what scry reported;
/// keeping them visible is what makes the next such correction cheap.
/// </remarks>
internal sealed record ScriptInstanceChain
{
    internal ScriptInstanceChain(
        NativePtr node,
        ScriptInstanceStatus status,
        NativePtr scriptInstance = default,
        NativePtr owner = default,
        NativePtr gcHandleSlot = default,
        ManagedPtr managedObject = default)
    {
        Node = node;
        Status = status;
        ScriptInstance = scriptInstance;
        Owner = owner;
        GcHandleSlot = gcHandleSlot;
        ManagedObject = managedObject;
    }

    /// <summary>The <c>Node*</c> the walk started from.</summary>
    public NativePtr Node { get; }

    /// <summary>How far the walk got, and why it stopped.</summary>
    public ScriptInstanceStatus Status { get; }

    /// <summary>
    /// <c>*(node + NodeScriptInstance)</c> — Godot's <c>ScriptInstance*</c>. Explicitly <b>not</b> the
    /// managed object, despite scry naming the accessor <c>getDotNetCoreObject</c> (§4.6).
    /// </summary>
    public NativePtr ScriptInstance { get; }

    /// <summary>
    /// <c>*(scriptInstance + ScriptInstanceOwner)</c> — the back-reference to the owning node. Equal
    /// to <see cref="Node"/> whenever <see cref="Status"/> is <see cref="ScriptInstanceStatus.Ok"/>;
    /// kept on the record so a mismatch can be reported with both values.
    /// </summary>
    public NativePtr Owner { get; }

    /// <summary>
    /// <c>*(scriptInstance + ScriptInstanceGcHandle)</c> — the GCHandle. Live capture (§4.6):
    /// <c>0x1a90351dd30 + 0x20</c> read <c>0x1a96f941360</c>, which is a handle <em>slot</em>, not the
    /// object.
    /// </summary>
    public NativePtr GcHandleSlot { get; }

    /// <summary>
    /// <c>*(gcHandleSlot)</c> — the managed C# object. Live capture: <c>0x1a974c6c240</c>, exactly
    /// what scry reported for <c>NGame</c>.
    /// </summary>
    public ManagedPtr ManagedObject { get; }

    /// <summary>The chain completed and the owner back-reference agreed.</summary>
    public bool IsResolved => Status == ScriptInstanceStatus.Ok;

    /// <summary>
    /// The node genuinely has no C# object behind it. Callers iterating a whole tree should treat
    /// this as "skip", not as a failure — most Godot nodes carry no script.
    /// </summary>
    public bool IsScriptless => Status is ScriptInstanceStatus.NoScriptInstance or ScriptInstanceStatus.NoGcHandle;

    /// <summary>
    /// Something was wrong with the memory we read: a failed read, a misaligned pointer, or — worst —
    /// an owner back-reference naming a different node.
    /// </summary>
    public bool IsSuspect => Status is ScriptInstanceStatus.ReadFailed
        or ScriptInstanceStatus.OwnerMismatch
        or ScriptInstanceStatus.SuspectHandle
        or ScriptInstanceStatus.SuspectScriptInstance;

    /// <inheritdoc/>
    public override string ToString() => Status switch
    {
        ScriptInstanceStatus.Ok => $"{Status}: {Node} -> {ScriptInstance} -> {GcHandleSlot} -> {ManagedObject}",
        ScriptInstanceStatus.OwnerMismatch => $"{Status}: {ScriptInstance} claims owner {Owner}, expected {Node}",
        _ => $"{Status}: {Node}",
    };
}
