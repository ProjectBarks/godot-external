using Godot.External.Abi;

namespace Godot.External.Bridge;

/// <summary>
/// The managed&#8596;native crossing, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two directions are not symmetric</b>, and the asymmetry is the whole content of
/// docs/analysis.md §4.6's bridge section:
/// </para>
/// <code>
/// managed -> native   ONE hop.  managedObject + offsetof(NativePtr)  ->  Node*
/// native  -> managed  THREE.    Node* + NodeScriptInstance           ->  ScriptInstance*
///                               ScriptInstance + ScriptInstanceOwner ->  owning Node*   (must match)
///                               ScriptInstance + ScriptInstanceGcHandle -> GCHandle
///                               *(GCHandle)                          ->  managed object
/// </code>
/// <para>
/// scry's accessor for the second direction is called <c>getDotNetCoreObject</c> and reads a single
/// pointer at <c>node + 0x68</c>. <b>That is wrong.</b> §12.3b tested it live and it was the one
/// failing check in an otherwise 111/112 pass; the failure is what exposed the real chain. The value
/// at <c>0x68</c> is Godot's <c>ScriptInstance*</c>, and it is a perfectly plausible heap pointer, so
/// the mistake does not announce itself.
/// </para>
/// <para>
/// The owner back-reference at <c>+0x08</c> is therefore checked on every crossing and a mismatch is
/// a <em>failure</em>, never a returned address. §8.8 makes this more than pedantry: Godot frees
/// nodes and reuses the allocation, so a stale <c>Node*</c> can address a different, entirely
/// plausible-looking node — and the back-reference is the cheapest available evidence that the
/// <c>ScriptInstance</c> we found belongs to the node we asked about.
/// </para>
/// </remarks>
internal sealed class GodotObjectBridge
{
    /// <summary>
    /// Name of the field a C# <c>GodotObject</c> carries the engine pointer in. §4.6: "a managed
    /// <c>GodotObject</c> carries the engine pointer in a field literally named <c>NativePtr</c>".
    /// </summary>
    public const string NativePointerFieldName = "NativePtr";

    private readonly IByteSource _source;

    /// <summary>Creates a bridge over a caller-supplied reader.</summary>
    /// <param name="read">Remote read primitive; see <see cref="RemoteRead"/>.</param>
    /// <param name="is64Bit">Pointer width of the target process.</param>
    /// <param name="profile">
    /// ABI profile supplying <c>NodeScriptInstance</c>, <c>ScriptInstanceOwner</c> and
    /// <c>ScriptInstanceGcHandle</c>. No offset in this class is hardcoded.
    /// </param>
    public GodotObjectBridge(RemoteRead read, bool is64Bit, GodotAbiProfile profile)
        : this(new RemoteReadByteSource(read, is64Bit), profile)
    {
    }

    internal GodotObjectBridge(IByteSource source, GodotAbiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);

        _source = source;
        Profile = profile;
    }

    /// <summary>The ABI profile every offset in this class comes from.</summary>
    public GodotAbiProfile Profile { get; }

    /// <summary>
    /// Walks <c>Node* &#8594; ScriptInstance* &#8594; GCHandle &#8594; managed object</c>, verifying the
    /// owner back-reference on the way.
    /// </summary>
    /// <param name="node">The engine <c>Node*</c>.</param>
    /// <returns>
    /// Every hop plus a <see cref="ScriptInstanceStatus"/>. Never throws: a suspect chain is data, so
    /// a polling overlay can fall back to its last good read (§8.8, "Error model").
    /// </returns>
    public ScriptInstanceChain ResolveManagedObject(NativePtr node)
    {
        GodotOffsetTable offsets = Profile.Offsets;
        int pointerSize = ByteSourceExtensions.PointerWidth;

        if (node.IsNull)
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.NoScriptInstance);
        }

        if (!_source.TryReadPointer(node.AtOffset(offsets.NodeScriptInstance).Address, out ulong scriptInstanceRaw))
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.ReadFailed);
        }

        NativePtr scriptInstance = new(scriptInstanceRaw);
        if (scriptInstance.IsNull)
        {
            // Most nodes in a Godot tree have no script at all. Not an error.
            return new ScriptInstanceChain(node, ScriptInstanceStatus.NoScriptInstance);
        }

        if (!scriptInstance.IsAligned(pointerSize))
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.SuspectScriptInstance, scriptInstance);
        }

        // The self-check §4.6 recommends, promoted to a hard gate: if this ScriptInstance does not
        // name our node as its owner, we are not looking at our node's ScriptInstance, and anything
        // we dereference from here would be a plausible lie.
        if (!_source.TryReadPointer(scriptInstance.AtOffset(offsets.ScriptInstanceOwner).Address, out ulong ownerRaw))
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.ReadFailed, scriptInstance);
        }

        NativePtr owner = new(ownerRaw);
        if (owner != node)
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.OwnerMismatch, scriptInstance, owner);
        }

        if (!_source.TryReadPointer(scriptInstance.AtOffset(offsets.ScriptInstanceGcHandle).Address, out ulong handleRaw))
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.ReadFailed, scriptInstance, owner);
        }

        NativePtr handleSlot = new(handleRaw);
        if (handleSlot.IsNull)
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.NoGcHandle, scriptInstance, owner);
        }

        // Deliberately NOT masking the low bits. Some GCHandle encodings tag them, but the live
        // capture in §4.6 was aligned, and masking would silently repair exactly the wrong-pointer
        // case this method exists to catch. If a future runtime tags handles, that must be a
        // measured change to this line, not an assumption baked in ahead of evidence.
        if (!handleSlot.IsAligned(pointerSize))
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.SuspectHandle, scriptInstance, owner, handleSlot);
        }

        if (!_source.TryReadPointer(handleSlot.Address, out ulong managedRaw))
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.ReadFailed, scriptInstance, owner, handleSlot);
        }

        ManagedPtr managed = new(managedRaw);
        if (managed.IsNull || (managedRaw & (ulong)(pointerSize - 1)) != 0)
        {
            return new ScriptInstanceChain(node, ScriptInstanceStatus.SuspectHandle, scriptInstance, owner, handleSlot, managed);
        }

        return new ScriptInstanceChain(node, ScriptInstanceStatus.Ok, scriptInstance, owner, handleSlot, managed);
    }

    /// <summary>
    /// Reads <c>NativePtr</c> off a managed <c>GodotObject</c>, asking
    /// <paramref name="resolver"/> where the field lives.
    /// </summary>
    /// <param name="managedObject">Address of the managed object.</param>
    /// <param name="resolver">The CLR-side seam; see <see cref="IManagedFieldOffsetResolver"/>.</param>
    /// <param name="fieldName">Field to read. Defaults to <see cref="NativePointerFieldName"/>.</param>
    public NativePointerResolution ResolveNativePointer(
        ManagedPtr managedObject,
        IManagedFieldOffsetResolver resolver,
        string fieldName = NativePointerFieldName)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrEmpty(fieldName);

        if (managedObject.IsNull)
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.NullPointer);
        }

        if (!resolver.TryGetInstanceFieldOffset(managedObject, fieldName, out int offset))
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.FieldNotResolved);
        }

        return ResolveNativePointerAt(managedObject, offset);
    }

    /// <summary>
    /// Reads the engine pointer from a known field offset, for callers that have already resolved
    /// the layout (LiveClr caches field descriptors per <c>MethodTable</c> — §12.4, API fact 1).
    /// </summary>
    /// <param name="managedObject">Address of the managed object.</param>
    /// <param name="fieldOffset">Byte offset of the <c>NativePtr</c> field, object header included.</param>
    public NativePointerResolution ResolveNativePointerAt(ManagedPtr managedObject, int fieldOffset)
    {
        if (managedObject.IsNull)
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.NullPointer);
        }

        if (fieldOffset < 0)
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.FieldNotResolved);
        }

        if (!_source.TryReadPointer(managedObject.AtOffset(fieldOffset).Address, out ulong raw))
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.ReadFailed, fieldOffset: fieldOffset);
        }

        NativePtr native = new(raw);
        if (native.IsNull)
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.NullPointer, fieldOffset: fieldOffset);
        }

        if (!native.IsAligned(ByteSourceExtensions.PointerWidth))
        {
            return new NativePointerResolution(managedObject, NativePointerStatus.Misaligned, native, fieldOffset);
        }

        return new NativePointerResolution(managedObject, NativePointerStatus.Ok, native, fieldOffset);
    }
}
