using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Scene;

namespace Godot.External.Tests;

/// <summary>
/// Lifetime rules for native pointers (docs/analysis.md §8.8, "Lifetimes — corrected").
/// </summary>
/// <remarks>
/// The hazard is not that a pointer becomes unreadable — it is that it stays readable and starts
/// describing a different node, because "Godot can free a node and reuse the allocation". These
/// tests pin that a handle from an ended epoch throws instead of answering.
/// </remarks>
public class SceneEpochTests
{
    [Fact]
    public void EndedEpoch_MakesEveryHandleThrow_RatherThanReadTheNewOccupant()
    {
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");
        ulong child = scene.NewNode("MainMenu", root);
        scene.SetChildren(root, child);

        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));
        Assert.True(node.TryGetName(out string before));
        Assert.Equal("Game", before);

        epoch.End();

        Assert.Throws<SceneEpochExpiredException>(() => { node.TryGetName(out _); });
        Assert.Throws<SceneEpochExpiredException>(() => { node.TryGetParent(out _); });
        Assert.Throws<SceneEpochExpiredException>(() => node.GetChildren());
        Assert.Throws<SceneEpochExpiredException>(() => node.ResolveManagedObject());
        Assert.Throws<SceneEpochExpiredException>(() => node.Classify());
    }

    [Fact]
    public void EndedEpoch_StillHoldsTheAddress_WhichIsPreciselyTheDanger()
    {
        // The address is fine, readable, and now meaningless. Nothing about the bytes says so; only
        // the epoch does.
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");

        SceneEpoch epoch = scene.BeginEpoch();
        GodotNode node = epoch.Node(new NativePtr(root));
        epoch.Dispose();

        Assert.Equal(new NativePtr(root), node.Address);
        Assert.True(epoch.HasEnded);

        // A fresh epoch over the same memory works: re-resolving is the supported recovery.
        using SceneEpoch next = scene.BeginEpoch();
        Assert.True(next.Node(new NativePtr(root)).TryGetName(out string name));
        Assert.Equal("Game", name);
    }

    [Fact]
    public void HandlesFromDifferentEpochs_AreNotEqual_EvenAtTheSameAddress()
    {
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");

        using SceneEpoch first = scene.BeginEpoch();
        using SceneEpoch second = scene.BeginEpoch();

        GodotNode a = first.Node(new NativePtr(root));
        GodotNode b = second.Node(new NativePtr(root));

        Assert.NotEqual(a, b);
        Assert.Equal(a, first.Node(new NativePtr(root)));
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void EndIsIdempotent()
    {
        SyntheticScene scene = new();
        SceneEpoch epoch = scene.BeginEpoch();

        epoch.End();
        epoch.End();
        epoch.Dispose();

        Assert.True(epoch.HasEnded);
    }

    [Fact]
    public void TakingAHandleFromAnEndedEpoch_Throws()
    {
        SyntheticScene scene = new();
        SceneEpoch epoch = scene.BeginEpoch();
        epoch.End();

        Assert.Throws<SceneEpochExpiredException>(() => epoch.Node(new NativePtr(0x1000)));
        Assert.Throws<SceneEpochExpiredException>(() => epoch.ControlUnchecked(new NativePtr(0x1000)));
        Assert.Throws<SceneEpochExpiredException>(() => epoch.SceneFrom(new NativePtr(0x1000)));
    }

    [Fact]
    public void ThirtyTwoBitTarget_IsRefused_NotServedAGuessedLayout()
    {
        FakeByteSource source = new() { Is64Bit = false };

        Assert.Throws<NotSupportedException>(
            () => new SceneEpoch(source, GodotAbiProfiles.Godot451Release));
    }

    [Fact]
    public void BeginOverADelegate_KeepsTheLiveClrSeamToOneMethod()
    {
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");

        RemoteRead read = scene.Source.TryRead;
        using SceneEpoch epoch = SceneEpoch.Begin(read, is64Bit: true, GodotAbiProfiles.Godot451Release);

        Assert.True(epoch.Node(new NativePtr(root)).TryGetName(out string name));
        Assert.Equal("Game", name);
    }
}
