using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Scene;

namespace Godot.External.Tests;

/// <summary>
/// Builds a Godot scene tree inside a <see cref="FakeByteSource"/>, laid out at the live-validated
/// release offsets, so the Bridge/Scene/Objects layers can be exercised without a running game.
/// </summary>
/// <remarks>
/// <para>
/// Every structure here is the one docs/analysis.md describes: the intrusive child list
/// (<c>next</c> at <c>+0x00</c>, payload at <c>+0x18</c>), <c>StringName</c>'s two indirections,
/// <c>CowData</c>'s length stored <em>before</em> the buffer, and the
/// <c>ScriptInstance</c>/GCHandle chain with its owner back-reference (§4.6).
/// </para>
/// <para>
/// <b>Non-Control nodes are built by writing pointers over the Control geometry offsets.</b> That is
/// not an arbitrary choice of garbage — it reproduces §12.4c, where Control accessors on an
/// <c>AudioStreamPlayer</c> returned denormals such as <c>2.6e-38</c>: an x64 heap pointer's high
/// half decodes as a near-denormal float and its low half as a wildly out-of-range one.
/// </para>
/// </remarks>
internal sealed class SyntheticScene
{
    private ulong _nextNode = 0x0010_0000;
    private ulong _nextAux = 0x4000_0000;

    /// <param name="densePages">
    /// Map whole 4 KiB pages rather than individual bytes. Required by anything that installs a
    /// <c>MemorySnapshot</c>: a page- or span-granular fetch reads across the padding between the
    /// fields written here, which a byte-sparse image refuses.
    /// </param>
    public SyntheticScene(bool densePages = false) => Source = new FakeByteSource { DensePages = densePages };

    /// <summary>Backing memory.</summary>
    public FakeByteSource Source { get; }

    /// <summary>The live-validated release profile (§12.3, §12.3b, §12.4c).</summary>
    public GodotAbiProfile Profile { get; } = GodotAbiProfiles.Godot451Release;

    /// <summary>Shorthand for the profile's offsets.</summary>
    public GodotOffsetTable Offsets => Profile.Offsets;

    /// <summary>Opens an epoch over this synthetic process.</summary>
    public SceneEpoch BeginEpoch(INodeClassifier? classifier = null) => new(Source, Profile, classifier);

    /// <summary>Reserves an address range that no node occupies.</summary>
    public ulong Alloc(int size)
    {
        ulong address = _nextAux;
        _nextAux += (ulong)Math.Max(size, 0x40);
        return address;
    }

    /// <summary>
    /// Creates a node: parent pointer, empty child list, no script, a name, and geometry that either
    /// reads as a <c>Control</c> or does not.
    /// </summary>
    public ulong NewNode(
        string name,
        ulong parent = 0,
        bool control = true,
        (float X, float Y)? size = null,
        (float X, float Y)? position = null)
    {
        ulong node = _nextNode;
        _nextNode += 0x1_0000; // comfortably past RichTextLabel::text at 0xa78

        Source.WritePointer(node + (ulong)Offsets.NodeParent, parent);
        Source.WritePointer(node + (ulong)Offsets.NodeChildListHead, 0);
        Source.WritePointer(node + (ulong)Offsets.NodeScriptInstance, 0);
        SetName(node, name);

        if (control)
        {
            SetControlGeometry(node, size ?? (100f, 50f), position ?? (0f, 0f));
        }
        else
        {
            SetNonControlBytes(node);
        }

        return node;
    }

    /// <summary>Writes <c>Node::name</c> as a <c>StringName</c>: node &#8594; _Data &#8594; +8 &#8594; UTF-32 buffer.</summary>
    public void SetName(ulong node, string name)
    {
        ulong data = Alloc(0x20);
        ulong buffer = Alloc(0x400);

        Source.WritePointer(node + (ulong)Offsets.NodeName, data);
        Source.WritePointer(data + (ulong)Offsets.StringNameDataToBuffer, buffer);
        Source.WriteGodotString(buffer, name);
    }

    /// <summary>Writes plausible <c>Control</c> geometry at the release offsets.</summary>
    public void SetControlGeometry(
        ulong node,
        (float X, float Y) size,
        (float X, float Y) position,
        (float X, float Y)? scale = null,
        float[]? offsets = null,
        bool visible = true,
        (float X, float Y) cachedGlobal = default)
    {
        (float sx, float sy) = scale ?? (1f, 1f);
        float[] anchorOffsets = offsets ?? [0f, 0f, 0f, 0f];

        Source.WriteSingle(node + (ulong)Offsets.ControlSize, size.X);
        Source.WriteSingle(node + (ulong)Offsets.ControlSize + 4, size.Y);
        Source.WriteSingle(node + (ulong)Offsets.ControlPosition, position.X);
        Source.WriteSingle(node + (ulong)Offsets.ControlPosition + 4, position.Y);
        Source.WriteSingle(node + (ulong)Offsets.ControlScale, sx);
        Source.WriteSingle(node + (ulong)Offsets.ControlScale + 4, sy);
        Source.WriteSingle(node + (ulong)Offsets.ControlGlobalPosition, cachedGlobal.X);
        Source.WriteSingle(node + (ulong)Offsets.ControlGlobalPosition + 4, cachedGlobal.Y);
        Source.WriteBytes(node + (ulong)Offsets.CanvasItemVisible, [visible ? (byte)1 : (byte)0]);

        for (int i = 0; i < 4; i++)
        {
            Source.WriteSingle(node + (ulong)(Offsets.ControlOffsets + (i * 4)), anchorOffsets[i]);
        }
    }

    /// <summary>
    /// Fills the Control geometry offsets with heap pointers, the way a non-<c>Control</c> node's
    /// unrelated members actually would. Reads succeed; the values are denormal or astronomical.
    /// </summary>
    public void SetNonControlBytes(ulong node)
    {
        ulong fakePointer = 0x0000_01A9_204C_5580; // the shape of a real x64 Godot heap pointer

        Source.WritePointer(node + (ulong)Offsets.ControlSize, fakePointer);
        Source.WritePointer(node + (ulong)Offsets.ControlPosition, fakePointer);
        Source.WritePointer(node + (ulong)Offsets.ControlScale, fakePointer);
        Source.WritePointer(node + (ulong)Offsets.ControlGlobalPosition, fakePointer);
        Source.WriteBytes(node + (ulong)Offsets.CanvasItemVisible, [1]);

        for (int i = 0; i < 4; i++)
        {
            Source.WriteSingle(node + (ulong)(Offsets.ControlOffsets + (i * 4)), 0f);
        }
    }

    /// <summary>
    /// Links <paramref name="children"/> onto <paramref name="parent"/> as Godot's intrusive list,
    /// and points each child's parent pointer back. Returns the link-node addresses so a test can
    /// splice or corrupt the chain.
    /// </summary>
    public ulong[] SetChildren(ulong parent, params ulong[] children)
    {
        ulong[] links = new ulong[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            links[i] = Alloc(0x40);
        }

        for (int i = 0; i < children.Length; i++)
        {
            ulong next = i + 1 < children.Length ? links[i + 1] : 0;
            Source.WritePointer(links[i] + (ulong)Offsets.ChildLinkNext, next);
            Source.WritePointer(links[i] + (ulong)Offsets.ChildLinkPayload, children[i]);
            Source.WritePointer(children[i] + (ulong)Offsets.NodeParent, parent);
        }

        Source.WritePointer(parent + (ulong)Offsets.NodeChildListHead, links.Length > 0 ? links[0] : 0);
        return links;
    }

    /// <summary>
    /// Wires the <c>ScriptInstance</c> chain: <c>node + 0x68</c> &#8594; ScriptInstance, whose
    /// <c>+0x08</c> names its owner and whose <c>+0x20</c> holds a GCHandle pointing at the managed
    /// object.
    /// </summary>
    /// <param name="ownerOverride">
    /// Written into the owner back-reference instead of <paramref name="node"/> — the mismatch case
    /// that §4.6's self-check exists to catch.
    /// </param>
    public (ulong ScriptInstance, ulong HandleSlot, ulong Managed) SetScriptInstance(
        ulong node,
        ulong scriptInstance,
        ulong handleSlot,
        ulong managed,
        ulong? ownerOverride = null)
    {
        Source.WritePointer(node + (ulong)Offsets.NodeScriptInstance, scriptInstance);
        Source.WritePointer(scriptInstance + (ulong)Offsets.ScriptInstanceOwner, ownerOverride ?? node);
        Source.WritePointer(scriptInstance + (ulong)Offsets.ScriptInstanceGcHandle, handleSlot);
        Source.WritePointer(handleSlot, managed);
        return (scriptInstance, handleSlot, managed);
    }

    /// <summary>Writes <c>Label::text</c> as a CowData buffer pointer.</summary>
    public void SetLabelText(ulong node, string text) => SetTextField(node, Offsets.LabelText, text);

    /// <summary>Writes <c>RichTextLabel::text</c> as a CowData buffer pointer.</summary>
    public void SetRichTextLabelText(ulong node, string text) => SetTextField(node, Offsets.RichTextLabelText, text);

    private void SetTextField(ulong node, int fieldOffset, string text)
    {
        ulong buffer = Alloc(0x400);
        Source.WritePointer(node + (ulong)fieldOffset, buffer);
        Source.WriteGodotString(buffer, text);
    }
}
