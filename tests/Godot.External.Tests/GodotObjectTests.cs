using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// The typed wrappers: geometry, text, type identity, and the composition gate
/// (docs/analysis.md §4.6, §12.3, §12.3b, §12.4c).
/// </summary>
public class GodotObjectTests
{
    [Fact]
    public void ControlGeometry_ReadsTheLiveValidatedValues()
    {
        // BgContainer's own numbers from §12.3 — distinctive enough that a wrong offset cannot
        // coincidentally reproduce them.
        SyntheticScene scene = new();
        ulong node = scene.NewNode("BgContainer", control: false);
        scene.SetControlGeometry(
            node,
            size: (2560f, 1200f),
            position: (0f, 24f),
            scale: (1f, 1f),
            offsets: [-960f, -516f, 1600f, 684f],
            visible: true);

        using SceneEpoch epoch = scene.BeginEpoch();
        Assert.True(epoch.Node(new NativePtr(node)).TryAsControl(out GodotControl? control));

        Assert.Equal(new GodotVector2(2560, 1200), control.Size);
        Assert.Equal(new GodotVector2(0, 24), control.Position);
        Assert.Equal(new GodotVector2(1, 1), control.Scale);
        Assert.Equal(new double[] { -960, -516, 1600, 684 }, control.Offsets);
        Assert.True(control.Visible);
    }

    [Fact]
    public void CachedGlobalPosition_IsStale_ButComposedIsNot()
    {
        // §12.3: MainMenuTextButtons and ContinueButton both read [0,0] from the cache while having
        // real positions. The cached read succeeds — the value is simply a lie.
        SyntheticScene scene = new();
        ulong menu = scene.NewNode("MainMenu", size: (1920f, 1080f), position: (0f, 0f));
        ulong buttons = scene.NewNode("MainMenuTextButtons", menu, size: (269f, 450f), position: (642f, 609f));
        ulong continueButton = scene.NewNode("ContinueButton", buttons, size: (200f, 50f), position: (34f, 50f));
        scene.SetChildren(menu, buttons);
        scene.SetChildren(buttons, continueButton);

        using SceneEpoch epoch = scene.BeginEpoch();
        GodotControl button = epoch.ControlUnchecked(new NativePtr(continueButton));

        Assert.True(button.TryGetCachedGlobalPosition(out GodotVector2 cached));
        Assert.Equal(GodotVector2.Zero, cached);

        Assert.True(button.TryGetGlobalPosition(out ComposedGlobalPosition composed));
        Assert.Equal(new GodotVector2(676, 659), composed.Position);
        Assert.Equal(2, composed.AncestorsComposed);
        Assert.False(composed.StoppedAtNonControl);
    }

    [Fact]
    public void CompositionGate_RefusesANonControlAncestor()
    {
        // §4.6's composition trap. The root here is a plain Node whose bytes at the Control offsets
        // are pointers — exactly the §12.4c situation that yields denormals like 2.6e-38. The sum
        // must stop at the type boundary rather than adding whatever those bytes decode to.
        SyntheticScene scene = new();
        ulong plainRoot = scene.NewNode("Game", control: false);
        ulong panel = scene.NewNode("Panel", plainRoot, size: (400f, 300f), position: (20f, 5f));
        ulong leaf = scene.NewNode("Icon", panel, size: (64f, 64f), position: (3f, 2f));
        scene.SetChildren(plainRoot, panel);
        scene.SetChildren(panel, leaf);

        using SceneEpoch epoch = scene.BeginEpoch();
        GodotControl icon = epoch.ControlUnchecked(new NativePtr(leaf));

        Assert.True(icon.TryGetGlobalPosition(out ComposedGlobalPosition composed));
        Assert.Equal(new GodotVector2(23, 7), composed.Position);
        Assert.Equal(1, composed.AncestorsComposed);
        Assert.True(composed.StoppedAtNonControl);

        // And the ancestor really is refused by the classifier, not merely absent.
        Assert.Equal(GodotNodeClass.NotControl, epoch.Classify(new NativePtr(plainRoot)));
    }

    [Fact]
    public void CompositionWithoutTheGate_Fails_WhichIsWhyTheGateExists()
    {
        // Same tree, but with a classifier that waves everything through. The composition does not
        // quietly produce a wrong number — the plausibility backstop refuses it — but it certainly
        // does not produce the right one either.
        SyntheticScene scene = new();
        ulong plainRoot = scene.NewNode("Game", control: false);
        ulong panel = scene.NewNode("Panel", plainRoot, size: (400f, 300f), position: (20f, 5f));
        ulong leaf = scene.NewNode("Icon", panel, size: (64f, 64f), position: (3f, 2f));
        scene.SetChildren(plainRoot, panel);
        scene.SetChildren(panel, leaf);

        using SceneEpoch epoch = scene.BeginEpoch(new DelegateNodeClassifier((_, _) => GodotNodeClass.Control));
        GodotControl icon = epoch.ControlUnchecked(new NativePtr(leaf));

        Assert.False(icon.TryGetGlobalPosition(out ComposedGlobalPosition composed));
        Assert.Equal(default, composed);
    }

    [Fact]
    public void Classifier_RejectsZeroedMemory_BecauseScaleCannotBeZero()
    {
        // Padding and null pointers decode to 0.0, which is a legitimate size and position. Scale is
        // what separates a real Control from a quiet region of zeroes.
        SyntheticScene scene = new();
        ulong node = scene.NewNode("Zeroed", control: false);
        scene.SetControlGeometry(node, size: (0f, 0f), position: (0f, 0f), scale: (0f, 0f));

        using SceneEpoch epoch = scene.BeginEpoch();
        Assert.Equal(GodotNodeClass.NotControl, epoch.Classify(new NativePtr(node)));
    }

    [Fact]
    public void Classifier_ReportsUnknown_WhenGeometryCannotBeRead()
    {
        SyntheticScene scene = new();
        ulong node = scene.NewNode("Sparse");
        scene.Source.Unmap(node + (ulong)scene.Offsets.ControlSize, 8);

        using SceneEpoch epoch = scene.BeginEpoch();
        Assert.Equal(GodotNodeClass.Unknown, epoch.Classify(new NativePtr(node)));

        // Unknown is not a Control: guessing yes is the expensive direction.
        Assert.False(epoch.IsControl(new NativePtr(node)));
        Assert.False(epoch.Node(new NativePtr(node)).TryAsControl(out GodotControl? control));
        Assert.Null(control);
    }

    [Fact]
    public void LabelText_DecodesUtf32_AtTheLabelOffset()
    {
        // §12.3b recovered these live, including the game's own build string.
        SyntheticScene scene = new();
        ulong label = scene.NewNode("VersionLabel", size: (300f, 24f));
        scene.SetLabelText(label, "[v0.107.1] (2026.06.18)");

        using SceneEpoch epoch = scene.BeginEpoch();
        Assert.True(epoch.LabelUnchecked(new NativePtr(label)).TryGetText(out string text));
        Assert.Equal("[v0.107.1] (2026.06.18)", text);
    }

    [Fact]
    public void LabelText_IsNotTruncatedToBytes()
    {
        // §4.6: scry truncates each char32_t to a byte — "fine for ASCII, lossy for anything else".
        // Astral code points must survive as surrogate pairs.
        SyntheticScene scene = new();
        ulong label = scene.NewNode("Fancy", size: (300f, 24f));
        scene.SetLabelText(label, "Nibbits ✦ \U0001F525");

        using SceneEpoch epoch = scene.BeginEpoch();
        Assert.True(epoch.LabelUnchecked(new NativePtr(label)).TryGetText(out string text));
        Assert.Equal("Nibbits ✦ \U0001F525", text);
    }

    [Fact]
    public void RichTextLabel_ReadsItsOwnOffset_NotTheLabelOne()
    {
        // 0xa78 versus 0x800. Writing different strings at each proves the two types do not share a
        // field — a RichTextLabel read through GodotLabel would silently return the wrong text.
        SyntheticScene scene = new();
        ulong node = scene.NewNode("Description", size: (500f, 200f));
        scene.SetLabelText(node, "wrong field");
        scene.SetRichTextLabelText(node, "Connection Interrupted");

        using SceneEpoch epoch = scene.BeginEpoch();

        Assert.True(epoch.RichTextLabelUnchecked(new NativePtr(node)).TryGetText(out string rich));
        Assert.Equal("Connection Interrupted", rich);

        Assert.True(epoch.LabelUnchecked(new NativePtr(node)).TryGetText(out string plain));
        Assert.Equal("wrong field", plain);
    }

    [Fact]
    public void EmptyTextField_IsSuccessNotFailure()
    {
        SyntheticScene scene = new();
        ulong label = scene.NewNode("Blank", size: (10f, 10f));
        scene.Source.WritePointer(label + (ulong)scene.Offsets.LabelText, 0);

        using SceneEpoch epoch = scene.BeginEpoch();
        Assert.True(epoch.LabelUnchecked(new NativePtr(label)).TryGetText(out string text));
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void NodeReads_FollowACalibratedProfile_WithNoConstantsInThisLayer()
    {
        // §12.5: a calibrated offset must take effect everywhere without editing Objects/.
        SyntheticScene scene = new();
        GodotAbiProfile shifted = scene.Profile.WithCalibratedOffset(GodotField.ControlSize, 0x600);

        ulong node = scene.NewNode("Recalibrated", control: false);
        scene.Source.WriteSingle(node + 0x600, 1920f);
        scene.Source.WriteSingle(node + 0x604, 1080f);

        using SceneEpoch epoch = new(scene.Source, shifted);
        Assert.True(epoch.ControlUnchecked(new NativePtr(node)).TryGetSize(out GodotVector2 size));
        Assert.Equal(new GodotVector2(1920, 1080), size);
        Assert.Equal(AbiConfidence.Calibrated, shifted.Confidence);
    }

    [Fact]
    public void ScriptInstance_IsReachableFromANode_AndDistinctFromTheManagedObject()
    {
        SyntheticScene scene = new();
        ulong node = scene.NewNode("NGame", size: (1920f, 1080f));
        ulong scriptInstance = scene.Alloc(0x40);
        ulong handleSlot = scene.Alloc(0x10);
        ulong managed = scene.Alloc(0x10);
        scene.SetScriptInstance(node, scriptInstance, handleSlot, managed);

        using SceneEpoch epoch = scene.BeginEpoch();
        GodotNode handle = epoch.Node(new NativePtr(node));

        Assert.True(handle.TryGetScriptInstance(out NativePtr si));
        Assert.Equal(new NativePtr(scriptInstance), si);

        ScriptInstanceChain chain = handle.ResolveManagedObject();
        Assert.True(chain.IsResolved);
        Assert.Equal(new ManagedPtr(managed), chain.ManagedObject);
        Assert.NotEqual(si.Address, chain.ManagedObject.Address);
    }

    [Fact]
    public void FailedReads_ReturnFalse_TheyDoNotThrow()
    {
        // §8.8's error model: an overlay's answer to a suspect read is "reuse the last good
        // snapshot", which is impossible if the read path throws.
        SyntheticScene scene = new();
        ulong node = scene.NewNode("Vanishing", size: (10f, 10f));
        scene.Source.Unmap(node + (ulong)scene.Offsets.NodeName, 8);
        scene.Source.Unmap(node + (ulong)scene.Offsets.ControlSize, 8);

        using SceneEpoch epoch = scene.BeginEpoch();
        GodotControl control = epoch.ControlUnchecked(new NativePtr(node));

        Assert.False(control.TryGetName(out _));
        Assert.Null(control.Name);
        Assert.False(control.TryGetSize(out _));
        Assert.Null(control.Size);
    }
}
