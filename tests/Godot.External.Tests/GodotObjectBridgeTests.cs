using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Scene;

namespace Godot.External.Tests;

/// <summary>
/// The managed&#8596;native crossing (docs/analysis.md §4.6, §12.3b).
/// </summary>
/// <remarks>
/// §12.3b's 22nd check — <c>getDotNetCoreObject</c> read as a single pointer at <c>node + 0x68</c> —
/// is the one failure in the doc's 111/112, and it failed by <em>succeeding</em>: it returned a
/// plausible heap address that happened to be Godot's <c>ScriptInstance</c>. These tests pin the
/// corrected three-hop chain and, more importantly, pin that a mismatched owner back-reference
/// produces a failure rather than an address.
/// </remarks>
public class GodotObjectBridgeTests
{
    // The exact addresses §4.6 captured live for NGame, so a regression is comparable to the doc.
    private const ulong LiveNode = 0x1a9204c5580;
    private const ulong LiveScriptInstance = 0x1a90351dd30;
    private const ulong LiveHandleSlot = 0x1a96f941360;
    private const ulong LiveManagedObject = 0x1a974c6c240;

    [Fact]
    public void ScriptInstanceChain_ResolvesTheManagedObject_TwoHopsPastTheScriptInstance()
    {
        SyntheticScene scene = new();
        ulong node = LiveNode;
        scene.Source.WritePointer(node + (ulong)scene.Offsets.NodeParent, 0);
        scene.SetScriptInstance(node, LiveScriptInstance, LiveHandleSlot, LiveManagedObject);

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.Ok, chain.Status);
        Assert.True(chain.IsResolved);
        Assert.Equal(new NativePtr(LiveScriptInstance), chain.ScriptInstance);
        Assert.Equal(new NativePtr(node), chain.Owner);
        Assert.Equal(new NativePtr(LiveHandleSlot), chain.GcHandleSlot);
        Assert.Equal(new ManagedPtr(LiveManagedObject), chain.ManagedObject);
    }

    [Fact]
    public void ScriptInstance_IsNotTheManagedObject()
    {
        // The regression §12.3b actually found: node + 0x68 is a plausible pointer, and it is the
        // wrong one. Reading it as the answer would have silently produced this address.
        SyntheticScene scene = new();
        scene.Source.WritePointer(LiveNode + (ulong)scene.Offsets.NodeParent, 0);
        scene.SetScriptInstance(LiveNode, LiveScriptInstance, LiveHandleSlot, LiveManagedObject);

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(LiveNode));

        Assert.NotEqual(chain.ScriptInstance.Address, chain.ManagedObject.Address);
        Assert.NotEqual(chain.GcHandleSlot.Address, chain.ManagedObject.Address);
    }

    [Fact]
    public void OwnerBackReferenceMismatch_Fails_RatherThanReturningAPlausiblePointer()
    {
        // §4.6: "+0x08 back-reference is a cheap self-check that you followed the right pointer."
        // The managed object IS readable here — the chain refuses to hand it over anyway, because a
        // ScriptInstance naming a different owner is not this node's ScriptInstance.
        SyntheticScene scene = new();
        ulong node = LiveNode;
        ulong someOtherNode = 0x1a920000000;
        scene.Source.WritePointer(node + (ulong)scene.Offsets.NodeParent, 0);
        scene.SetScriptInstance(node, LiveScriptInstance, LiveHandleSlot, LiveManagedObject, ownerOverride: someOtherNode);

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.OwnerMismatch, chain.Status);
        Assert.False(chain.IsResolved);
        Assert.True(chain.IsSuspect);
        Assert.Equal(ManagedPtr.Null, chain.ManagedObject);

        // Both sides of the disagreement are reported, so the failure is diagnosable.
        Assert.Equal(new NativePtr(someOtherNode), chain.Owner);
        Assert.Equal(new NativePtr(node), chain.Node);
    }

    [Fact]
    public void NodeWithoutAScript_IsNotAnError()
    {
        SyntheticScene scene = new();
        ulong node = scene.NewNode("FmodBankLoader");

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.NoScriptInstance, chain.Status);
        Assert.True(chain.IsScriptless);
        Assert.False(chain.IsSuspect);
    }

    [Fact]
    public void NullGcHandleSlot_IsReportedDistinctlyFromNoScriptInstance()
    {
        SyntheticScene scene = new();
        ulong node = scene.NewNode("Proxy");
        ulong scriptInstance = scene.Alloc(0x40);
        scene.SetScriptInstance(node, scriptInstance, handleSlot: 0, managed: 0);

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.NoGcHandle, chain.Status);
        Assert.Equal(new NativePtr(scriptInstance), chain.ScriptInstance);
    }

    [Fact]
    public void UnreadableScriptInstance_ReportsReadFailed_NotOwnerMismatch()
    {
        SyntheticScene scene = new();
        ulong node = scene.NewNode("AudioManager");
        ulong scriptInstance = scene.Alloc(0x40);
        scene.Source.WritePointer(node + (ulong)scene.Offsets.NodeScriptInstance, scriptInstance);
        // The ScriptInstance itself is never mapped: reads through it fail.

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.ReadFailed, chain.Status);
        Assert.True(chain.IsSuspect);
    }

    [Fact]
    public void MisalignedScriptInstance_IsRefused()
    {
        SyntheticScene scene = new();
        ulong node = scene.NewNode("Torn");
        scene.Source.WritePointer(node + (ulong)scene.Offsets.NodeScriptInstance, 0x1a90351dd33);

        using SceneEpoch epoch = scene.BeginEpoch();
        ScriptInstanceChain chain = epoch.Bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.SuspectScriptInstance, chain.Status);
    }

    [Fact]
    public void ManagedToNative_ReadsNativePtrThroughTheResolver()
    {
        // §4.6, confirmed live: NGame.Instance -> NativePtr = 0x1a9204c5580 -> root node "Game".
        SyntheticScene scene = new();
        const int nativePtrFieldOffset = 0x18;
        scene.Source.WritePointer(LiveManagedObject + nativePtrFieldOffset, LiveNode);

        DelegateManagedFieldOffsetResolver resolver = new(
            (_, field) => field == GodotObjectBridge.NativePointerFieldName ? nativePtrFieldOffset : null);

        using SceneEpoch epoch = scene.BeginEpoch();
        NativePointerResolution resolution =
            epoch.Bridge.ResolveNativePointer(new ManagedPtr(LiveManagedObject), resolver);

        Assert.Equal(NativePointerStatus.Ok, resolution.Status);
        Assert.Equal(new NativePtr(LiveNode), resolution.NativePointer);
        Assert.Equal(nativePtrFieldOffset, resolution.FieldOffset);
    }

    [Fact]
    public void ManagedToNative_UnresolvedField_DoesNotGuessAnOffset()
    {
        // Guessing would hand back a plausible pointer: §4.6 records the managed address being
        // passed to the native accessors and resolving to the string "is_visible".
        SyntheticScene scene = new();
        DelegateManagedFieldOffsetResolver resolver = new((_, _) => null);

        using SceneEpoch epoch = scene.BeginEpoch();
        NativePointerResolution resolution =
            epoch.Bridge.ResolveNativePointer(new ManagedPtr(LiveManagedObject), resolver);

        Assert.Equal(NativePointerStatus.FieldNotResolved, resolution.Status);
        Assert.True(resolution.NativePointer.IsNull);
        Assert.Equal(-1, resolution.FieldOffset);
    }

    [Fact]
    public void ManagedToNative_MisalignedFieldValue_IsRefused()
    {
        SyntheticScene scene = new();
        scene.Source.WritePointer(LiveManagedObject + 0x18, LiveNode + 1);

        using SceneEpoch epoch = scene.BeginEpoch();
        NativePointerResolution resolution =
            epoch.Bridge.ResolveNativePointerAt(new ManagedPtr(LiveManagedObject), 0x18);

        Assert.Equal(NativePointerStatus.Misaligned, resolution.Status);
        Assert.False(resolution.IsResolved);
    }

    [Fact]
    public void ManagedToNative_NullNativePtr_IsNotCorruption()
    {
        SyntheticScene scene = new();
        scene.Source.WritePointer(LiveManagedObject + 0x18, 0);

        using SceneEpoch epoch = scene.BeginEpoch();
        NativePointerResolution resolution =
            epoch.Bridge.ResolveNativePointerAt(new ManagedPtr(LiveManagedObject), 0x18);

        Assert.Equal(NativePointerStatus.NullPointer, resolution.Status);
    }

    [Fact]
    public void EveryOffsetComesFromTheProfile_NotFromThisLayer()
    {
        // A calibrated profile (§12.5) must take effect without touching Bridge/. Move the whole
        // chain to different offsets and the bridge follows.
        SyntheticScene scene = new();
        GodotAbiProfile shifted = scene.Profile with
        {
            Offsets = scene.Offsets
                .With(GodotField.NodeScriptInstance, 0x70)
                .With(GodotField.ScriptInstanceOwner, 0x10)
                .With(GodotField.ScriptInstanceGcHandle, 0x30),
        };

        ulong node = 0x1a9204c5580;
        scene.Source.WritePointer(node + 0x70, LiveScriptInstance);
        scene.Source.WritePointer(LiveScriptInstance + 0x10, node);
        scene.Source.WritePointer(LiveScriptInstance + 0x30, LiveHandleSlot);
        scene.Source.WritePointer(LiveHandleSlot, LiveManagedObject);

        GodotObjectBridge bridge = new(scene.Source, shifted);
        ScriptInstanceChain chain = bridge.ResolveManagedObject(new NativePtr(node));

        Assert.Equal(ScriptInstanceStatus.Ok, chain.Status);
        Assert.Equal(new ManagedPtr(LiveManagedObject), chain.ManagedObject);
    }
}
