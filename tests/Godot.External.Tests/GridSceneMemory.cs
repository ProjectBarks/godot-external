using System.Buffers.Binary;
using System.Text;
using LiveClr.Memory;

namespace Godot.External.Tests;

/// <summary>One authored node of the grid scene, in the shape the calibrator has to rediscover.</summary>
internal sealed record GridNode(
    string Path,
    string Name,
    double[] Size,
    double[] Position,
    double[] Scale,
    double[] Offsets,
    double[] Anchors,
    bool Visible,
    string? Text,
    bool RichText = false,
    string Class = "Control");

/// <summary>
/// The <c>tools/godot-abi-grid</c> scene laid out in memory at the §4.6 release offsets, behind
/// LiveClr's <see cref="IMemoryReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the engine, not the calibrator: it is allowed to know every offset, and it writes them
/// the way a Godot build would. What makes it a test rather than a tautology is that the calibrator
/// is handed nothing but the values the harness sends — sizes, names, two visibility flags — and has
/// to find the offsets itself.
/// </para>
/// <para>
/// The decoys are the point of the fixture. §12.5 got four candidate size offsets from one control,
/// so <c>AlphaPanel</c> carries its size three times over; <c>CanvasItem::modulate</c> is
/// <c>Color(1,1,1,1)</c> and would otherwise look exactly like <c>scale</c>, so it is present;
/// <c>anchor[4]</c> sits immediately after <c>offset[4]</c> with a non-zero quad on
/// <c>AnchoredWide</c>, which is the tie-break the grid scene exists to provide; and every byte not
/// deliberately written is <c>0xCD</c>, which is neither a plausible float, nor an aligned pointer,
/// nor a boolean.
/// </para>
/// </remarks>
internal sealed class GridSceneMemory : IMemoryReader, Godot.External.Calibrator.Target.IRegionSource
{
    public const int NodeParent = 0x128;
    public const int NodeChildListHead = 0x148;
    public const int NodeName = 0x1c0;
    public const int NodeScriptInstance = 0x68;
    public const int CanvasItemVisible = 0x370;
    public const int CanvasItemModulate = 0x340;
    public const int ControlOffsets = 0x470;
    public const int ControlAnchors = 0x480;
    public const int ControlScale = 0x4a8;
    public const int ControlPivot = 0x4b0;
    public const int ControlPosition = 0x4b8;
    public const int ControlSize = 0x4c0;
    public const int LabelText = 0x7f8;   // text; xl_text shares its allocation at 0x800
    public const int RichTextLabelText = 0xa78;
    public const int ChildLinkNext = 0x00;
    public const int ChildLinkPayload = 0x18;
    public const int ScriptInstanceOwner = 0x08;
    public const int ScriptInstanceGcHandle = 0x20;

    private const int NodeBlock = 0x1400;
    private const byte Junk = 0xCD;

    private readonly List<(ulong Start, byte[] Bytes)> _blocks = [];
    private readonly List<(ulong Start, ulong Size)> _holes = [];
    private int _healAfter = int.MaxValue;
    private int _holeFailures;

    /// <summary>Failed reads that struck a hole; used to calibrate the healing threshold in tests.</summary>
    public int HoleFailures => _holeFailures;
    private ulong _nextNode = 0x0000_01A9_0000_0000;
    private ulong _nextAux = 0x0000_01A9_8000_0000;

    /// <summary>Node address by scene path.</summary>
    public Dictionary<string, ulong> ByPath { get; } = [];

    /// <summary>The authored scene.</summary>
    public IReadOnlyList<GridNode> Nodes { get; }

    /// <summary>Root node address.</summary>
    public ulong Root => ByPath[Nodes[0].Path];

    /// <summary>Address of the managed object the root's GCHandle points at.</summary>
    public ulong ManagedObject { get; private set; }

    /// <summary>Character-buffer address of each node name, by name.</summary>
    public Dictionary<string, ulong> NameBuffers { get; } = [];

    public bool Is64Bit => true;

    /// <summary>Every mapped block, so a whole-process scan can be exercised offline.</summary>
    public IEnumerable<Godot.External.Calibrator.Target.ScanRegion> Regions()
        => _blocks.OrderBy(b => b.Start).Select(b => new Godot.External.Calibrator.Target.ScanRegion(b.Start, (ulong)b.Bytes.Length));

    private readonly List<Action> _frameAdvance = [];
    private bool _dirtyPadding;
    private bool _secondRich;
    private bool _msvcRtti;

    /// <summary>Implementing class of the walk root's ScriptInstance.</summary>
    public string ScriptInstanceClass { get; init; } = "CSharpInstance";
    private bool _unsharedXlText;
    private bool _suppressRichText;

    /// <summary>
    /// Moves the target on by a frame: per-frame state changes, transient allocations go away.
    /// Wire this in as the session's <c>refresh</c> to model a running engine.
    /// </summary>
    public void AdvanceFrame()
    {
        foreach (Action step in _frameAdvance)
        {
            step();
        }
    }

    public GridSceneMemory(
        bool duplicateLabelText = false,
        bool strayNodeString = false,
        bool decoyScriptInstance = false,
        bool decoyNameData = false,
        bool sharedLabelConstant = false,
        bool nonControlSibling = false,
        bool transients = false,
        bool hiddenByAncestor = false,
        bool richTextDecoys = false,
        bool headerJunkString = false,
        bool suppressRichText = false,
        bool dirtyPadding = false,
        bool richTextTie = false,
        bool phantomTextOnPlainNodes = false,
        bool labelTextPlusStrayControl = false,
        bool oneLabelEmpty = false,
        bool noClassNames = false,
        bool oneClassPointerMissing = false,
        bool secondRichTextLabel = false,
        bool msvcRtti = false,
        bool unsharedXlText = false,
        bool scriptInstanceVanishes = false,
        bool unreadableParentWindow = false,
        bool transientParentHole = false,
        bool highScriptInstance = false,
        bool oneControlVetoes = false,
        bool controlLevelString = false)
    {
        _dirtyPadding = dirtyPadding;
        _secondRich = secondRichTextLabel;
        _msvcRtti = msvcRtti;
        _unsharedXlText = unsharedXlText;
        _suppressRichText = suppressRichText;
        Nodes = GridScene.Nodes;

        foreach (GridNode node in Nodes)
        {
            ByPath[node.Path] = Allocate(NodeBlock);
        }

        foreach (GridNode node in Nodes)
        {
            ulong address = ByPath[node.Path];
            WriteName(address, node.Name);
            WritePointer(address + NodeScriptInstance, 0);
            WritePointer(address + NodeChildListHead, 0);
            WritePointer(address + NodeParent, ParentOf(node) is string parent ? ByPath[parent] : 0);

            WriteReals(address + ControlOffsets, node.Offsets);
            WriteReals(address + ControlAnchors, node.Anchors);
            WriteReals(address + ControlScale, node.Scale);
            WriteReals(address + ControlPivot, [0, 0]);
            WriteReals(address + ControlPosition, node.Position);
            WriteReals(address + ControlSize, node.Size);
            WriteReals(address + CanvasItemModulate, [1, 1, 1, 1]);
            WriteCanvasItem(address, node.Visible, node.Visible);

            if (node.Text is string text)
            {
                if (node.RichText)
                {
                    if (!_suppressRichText)
                    {
                        WriteRichTextLabel(address, text);
                    }
                }
                else if (!oneLabelEmpty || node.Name != "ZetaLabelUnicode")
                {
                    WriteLabel(address, text);
                }
            }
        }

        foreach (GridNode node in Nodes)
        {
            LinkChildren(node);
        }

        if (!noClassNames)
        {
            WriteClassNames();
        }

        if (oneClassPointerMissing)
        {
            // One node whose class pointer cannot be resolved — a short window, an unmapped tail, an
            // interning this reader does not follow. Requiring EVERY node to answer let exactly this
            // withhold the class map for a whole engine version.
            WritePointer(ByPath["RootHarness/OmegaPanel/OmegaChild"], 0);
        }

        if (transientParentHole)
        {
            // The same hole, but it heals on the next attempt — a momentary read failure rather than
            // an unmapped page. One retry should make the whole cell survive it.
            // Heals only after the bisecting read has exhausted its own attempts, so what recovers
            // the cell is the retry pass and not Fill's halving.
            _healAfter = 40;
            Unmap(ByPath["RootHarness/AlphaPanel/BetaBranch"] + (ulong)NodeParent, 8);
        }

        if (unreadableParentWindow)
        {
            // A hole across the parent slot on one node. node.parent then cannot be derived — the
            // §12.5 coverage gate is doing its job — and nothing downstream may report a parent.
            Unmap(ByPath["RootHarness/AlphaPanel/BetaBranch"] + (ulong)NodeParent, 8);
        }

        if (scriptInstanceVanishes)
        {
            // The confirming reading finds nothing at all — a failed read, not a contradiction.
            _frameAdvance.Add(() => WritePointer(Root + (ulong)NodeScriptInstance, 0));
        }

                WriteScriptInstance(highScriptInstance ? 0x968 : NodeScriptInstance, highScriptInstance ? 0x10 : ScriptInstanceOwner);
        WriteDecoys();

        if (oneControlVetoes)
        {
            // One unrelated Control whose bytes do not satisfy the CanvasItem shape. Under the old
            // universality rule this single node vetoed the true offset for the whole scene.
            Write(ByPath["RootHarness/OmegaPanel/OmegaChild"] + (ulong)CanvasItemVisible, [0x7F]);
        }

        if (controlLevelString)
        {
            // A String that every Control has — the shape of a Control-level member sitting in the
            // several hundred bytes of unmeasured internals the floor still admits. It covers far
            // more nodes than any Label field ever could, which is the only thing that gives it away.
            foreach (GridNode node in Nodes)
            {
                WriteRichTextLabelAt(ByPath[node.Path], 0x600, "control-level:" + node.Name);
            }
        }

        if (labelTextPlusStrayControl)
        {
            // The harder phantom: an offset that decodes BOTH Labels correctly and also happens to
            // hold a valid CowData on the walk root. Set size cannot catch it — three of twenty is no
            // majority — and neither can any check on the string, which is a perfectly good one. Only
            // the engine's own class names can.
            foreach (string path in LabelPaths)
            {
                WriteLabelAt(ByPath[path], 0x8f8, "shared-with-root");
            }

            WriteLabelAt(Root, 0x8f8, "res://Probe.gd");
        }

        if (phantomTextOnPlainNodes)
        {
            // Two bracket-passing strings on two DIFFERENT text-less Controls, at two different
            // offsets. Each forms its own single-node group, so the groups tie and the class is
            // undecided — and each node is then covered by exactly ONE candidate, where "all
            // candidates agree" is trivially true over a set of one. That is how "res://Probe.gd"
            // and "@implicit_new" reached published results.
            WriteRichTextLabelAt(ByPath["RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore"], 0x700, "res://Probe.gd");
            WriteRichTextLabelAt(ByPath["RootHarness/AlphaPanel/AlphaLeaf"], 0x780, "@implicit_new");
        }

        if (richTextTie)
        {
            // Exactly one rival candidate, decoding something else: two candidates, one vote each,
            // nothing to prefer between them.
            WriteRichTextLabelAt(
                ByPath["RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich"],
                0xc28,
                "not the authored text");
        }

        if (strayNodeString)
        {
            // A one-off string on a Label that is not its text: a scene path, a font name. Real
            // targets are full of these.
            WriteString(ByPath["RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelAscii"] + 0x900, "res://Main.tscn");
        }

        if (decoyScriptInstance)
        {
            // A second structure that also points back at the node, at a different distance. On a
            // release scene where only the root is scripted, pointer identity cannot separate this
            // from the real ScriptInstance.
            ulong impostor = Allocate(0x40);
            WritePointer(Root + 0x80, impostor);
            WritePointer(impostor + 0x10, Root);
        }

        if (sharedLabelConstant)
        {
            // A font path: decodable, non-empty, at a fixed offset, and on exactly the two Label
            // nodes — the same node set as `text`. It is IDENTICAL on both, which is the only thing
            // separating it from authored text.
            foreach (string path in LabelPaths)
            {
                WriteString(ByPath[path] + 0x880, "res://theme/GridDefault.tres");
            }
        }

        if (nonControlSibling)
        {
            // §12.4c: a non-Control node read through Control offsets. The high half of an x64 heap
            // pointer decodes as a near-denormal float, and one field is Infinity, which used to take
            // the whole cell down on the way out through JSON.
            ulong plain = ByPath["RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaSiblingOne"];
            foreach (int field in new[] { ControlSize, ControlPosition, ControlScale, ControlOffsets })
            {
                WritePointer(plain + (ulong)field, 0x0000_01A9_204C_5580);
            }

            Span<byte> infinity = stackalloc byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(infinity, float.PositiveInfinity);
            Write(plain + ControlSize, infinity);
            Write(plain + CanvasItemVisible, [1]);
        }

        if (hiddenByAncestor)
        {
            // Hidden because an ANCESTOR is hidden, not by its own hide(). The stored `visible` is
            // then IDENTICAL on both twins and the byte that differs is parent_visible_in_tree, one
            // byte later — so a difference nominates the byte before it as well.
            WriteCanvasItem(ByPath["RootHarness/AlphaPanel/BetaBranch/HiddenTwin"], visible: true, parentVisibleInTree: false);
        }

        if (headerJunkString)
        {
            // A genuine Godot String inside the Node/Object header — the shape of a StringName's
            // character buffer, which every node has several of. It passes every CowData check
            // because it IS a CowData, and it satisfies the RichTextLabel bracket by coincidence.
            // Only its position disqualifies it: no Label or RichTextLabel member can live below
            // node.parent.
            WriteRichTextLabelAt(ByPath["RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich"], 0x110, "Color");
        }

        if (richTextDecoys)
        {
            // Three bracket-passing candidates on the RichTextLabel, two of which decode the authored
            // string and one of which does not. Measured on a real cell, where two candidates decoded
            // the expected text exactly and were vetoed by two that did not.
            ulong rich = ByPath["RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich"];
            WriteRichTextLabelAt(rich, 0xc28, "ρich ✦ テキスト 𝄞 RTL");
            WriteRichTextLabelAt(rich, 0xd30, "not the authored text");
        }

        if (transients)
        {
            WriteTransients();
        }

        if (decoyNameData)
        {
            // A structure that reaches the root's character buffer at a DIFFERENT distance than
            // StringName::_Data does, and a slot pointing at it. This is the shape that made root
            // location depend on dictionary ordering: measured live, RootHarness validated at k=24
            // while its children validated at k=8.
            ulong impostor = Allocate(0x40);
            WritePointer(impostor + 24, NameBuffers["RootHarness"]);
            WritePointer(Allocate(0x40), impostor);
        }
    }

    /// <summary>Removes a mapping, so reads through it fail — an unmapped page.</summary>
    public void Unmap(ulong address, int length)
    {
        foreach ((ulong start, byte[] bytes) in _blocks)
        {
            if (address >= start && address + (ulong)length <= start + (ulong)bytes.Length)
            {
                _holes.Add((address, (ulong)length));
                return;
            }
        }
    }

    public bool TryRead(ulong address, Span<byte> buffer)
    {
        for (int i = 0; i < _holes.Count; i++)
        {
            (ulong start, ulong size) = _holes[i];
            if (address < start + size && start < address + (ulong)buffer.Length)
            {
                if (++_holeFailures >= _healAfter)
                {
                    _holes.RemoveAt(i);
                }

                return false;
            }
        }

        foreach ((ulong start, byte[] bytes) in _blocks)
        {
            if (address >= start && address + (ulong)buffer.Length <= start + (ulong)bytes.Length)
            {
                bytes.AsSpan((int)(address - start), buffer.Length).CopyTo(buffer);
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
    }

    /// <summary>Child paths of <paramref name="path"/>, in authored order.</summary>
    public IEnumerable<GridNode> ChildrenOf(string path)
        => Nodes.Where(n => ParentOf(n) == path);

    private static string? ParentOf(GridNode node)
    {
        int slash = node.Path.LastIndexOf('/');
        return slash < 0 ? null : node.Path[..slash];
    }

    private void LinkChildren(GridNode node)
    {
        GridNode[] children = [.. ChildrenOf(node.Path)];
        if (children.Length == 0)
        {
            return;
        }

        ulong[] links = [.. children.Select(_ => Allocate(0x40))];
        for (int i = 0; i < children.Length; i++)
        {
            WritePointer(links[i] + ChildLinkNext, i + 1 < links.Length ? links[i + 1] : 0);
            WritePointer(links[i] + ChildLinkPayload, ByPath[children[i].Path]);
        }

        WritePointer(ByPath[node.Path] + NodeChildListHead, links[0]);
    }

    private void WriteScriptInstance(int slot, int ownerBackref)
    {
        ulong root = Root;
        ulong scriptInstance = Allocate(0x40);
        ulong handleSlot = Allocate(0x40);
        ManagedObject = Allocate(0x80);

        WritePointer(root + (ulong)slot, scriptInstance);
        // A real ScriptInstance is a polymorphic C++ object like any other, so it names its own
        // implementing class through the same RTTI the nodes use. That class — not the cell's
        // binding — is what decides where the owner back-reference sits.
        WritePointer(scriptInstance, WriteVtable(ScriptInstanceClass));
        WritePointer(scriptInstance + (ulong)ownerBackref, root);
        WritePointer(scriptInstance + ScriptInstanceGcHandle, handleSlot);
        WritePointer(handleSlot, ManagedObject);
    }

    /// <summary>
    /// Lays out <c>CanvasItem</c>'s boolean block the way Godot declares it — identical in 4.3 and
    /// 4.5, no <c>#ifdef</c>s.
    /// </summary>
    /// <remarks>
    /// The <c>Window *window</c> pointer eight bytes behind <c>visible</c> is the whole point of
    /// writing this out: it is what tells <c>visible</c> apart from <c>notify_local_transform</c>,
    /// the one other boolean in the block that also sits at a multiple of eight.
    /// </remarks>
    private void WriteCanvasItem(ulong node, bool visible, bool parentVisibleInTree)
    {
        ulong v = node + CanvasItemVisible;

        WriteUInt32(v - 16, 0);              // int z_index
        Write(v - 12, [1]);                  // bool z_relative
        Write(v - 11, [0]);                  // bool y_sort_enabled
        Write(v - 10, [0, 0]);               // padding
        WritePointer(v - 8, 0);              // Window *window

        Write(v, [visible ? (byte)1 : (byte)0]);
        Write(v + 1, [parentVisibleInTree ? (byte)1 : (byte)0]);
        Write(v + 2, [0]);                   // pending_update
        for (int i = 3; i <= 10; i++)
        {
            Write(v + (ulong)i, [(byte)(i % 2)]);
        }

        Write(v + 11, [0]);                  // padding
        WriteUInt32(v + 12, 0);              // clip_children_mode

        if (_dirtyPadding)
        {
            // C++ does not zero struct padding and Godot does not memset its objects, so these bytes
            // hold whatever the allocator last left there. A rule that requires them to be zero is
            // not a layout fact — and it eliminated the true offset on every cell of a full run.
            Write(v - 10, [0xAB, 0xCD]);
            Write(v + 11, [0x7F]);
            Write(v + 5, [5]);               // a small enum where a bool was assumed
            WriteUInt32(v + 12, 0x0BAD_F00D);
        }
    }

    /// <summary>
    /// Lays out <c>Label</c>: <c>String text</c> then <c>String xl_text</c>, which share one
    /// allocation whenever nothing is translated, bracketed by the alignment enums and autowrap.
    /// </summary>
    private void WriteLabel(ulong node, string text) => WriteLabelAt(node, LabelText, text);

    /// <summary>
    /// Lays out <c>RichTextLabel</c>, which has <b>no</b> <c>xl_text</c> member: one stored String,
    /// <c>use_bbcode</c> behind it and a run of bools after.
    /// </summary>
    private void WriteRichTextLabel(ulong node, string text) => WriteRichTextLabelAt(node, RichTextLabelText, text);

    private void WriteRichTextLabelAt(ulong node, int offset, string text)
    {
        WriteCowData(node + (ulong)offset, text);
        WriteUInt64(node + (ulong)offset - 8, 0);  // use_bbcode = false, plus padding
        WriteUInt64(node + (ulong)offset + 8, 0);
    }

    /// <summary>
    /// Writes <c>Object::_class_name_ptr</c>: one static <c>StringName</c> per class, shared by every
    /// instance of it, exactly as the engine interns them.
    /// </summary>
    private void WriteClassNames()
    {
        Dictionary<string, ulong> byClass = [];
        foreach (GridNode node in Nodes)
        {
            string effective = ClassOf(node);
            if (!byClass.TryGetValue(effective, out ulong vtable))
            {
                vtable = WriteVtable(effective);
                byClass[effective] = vtable;
            }

            WritePointer(ByPath[node.Path], vtable);
        }
    }

    /// <summary>
    /// Lays out one class's vtable with the Itanium RTTI that precedes it.
    /// </summary>
    /// <remarks>
    /// <c>offset-to-top</c> at <c>-16</c> and <c>type_info*</c> at <c>-8</c>, the type_info carrying
    /// its own vtable pointer at <c>+0</c> (which is what tells Itanium from MSVC) and the mangled,
    /// length-prefixed name at <c>+8</c>. Every offset here is an ABI constant, which is the whole
    /// reason this route needs no calibration.
    /// </remarks>
    /// <summary>Godot's single-inheritance chain, as the engine's own RTTI presents it.</summary>
    private static readonly Dictionary<string, string> BaseOf = new()
    {
        ["Node"] = "Object",
        ["CanvasItem"] = "Node",
        ["Control"] = "CanvasItem",
        ["Label"] = "Control",
        ["RichTextLabel"] = "Control",
        ["ColorRect"] = "Control",
        ["Panel"] = "Control",
        ["CSharpInstance"] = "ScriptInstance",
    };

    private readonly Dictionary<string, ulong> _typeInfoOf = [];

    private ulong WriteVtable(string className)
    {
        ulong block = Allocate(0x200);
        ulong vtable = block + 0x40;
        ulong typeInfo = WriteTypeInfo(className);

        WriteUInt64(vtable - 16, 0);                       // offset-to-top: a primary-base object
        WritePointer(vtable - 8, typeInfo);
        return vtable;
    }

    /// <summary>
    /// Writes one <c>std::type_info</c>, chaining <c>__base_type</c> at <c>+16</c> the way
    /// <c>__si_class_type_info</c> does, so the hierarchy is readable from the image rather than
    /// assumed by the reader.
    /// </summary>
    private ulong WriteTypeInfo(string className)
    {
        if (_typeInfoOf.TryGetValue(className, out ulong existing))
        {
            return existing;
        }

        ulong typeInfo = Allocate(0x40);
        ulong mangled = Allocate(0x80);
        _typeInfoOf[className] = typeInfo;
        // Itanium puts type_info's own vtable pointer here. MSVC puts a CompleteObjectLocator whose
        // first DWORD is a version number of 0 or 1 — which is what the ABI probe looks at, so a
        // custom MSVC-built template is declined rather than decoded into nonsense.
        WritePointer(typeInfo, _msvcRtti ? 1UL : 0x0000_7FF8_0000_0100);
        WritePointer(typeInfo + 8, mangled);

        string encoded = $"{className.Length}{className}"; // Itanium length prefix: "5Label"
        byte[] bytes = new byte[encoded.Length + 1];
        for (int i = 0; i < encoded.Length; i++)
        {
            bytes[i] = (byte)encoded[i];
        }

        Write(mangled, bytes);

        if (BaseOf.TryGetValue(className, out string? baseName))
        {
            WritePointer(typeInfo + 16, WriteTypeInfo(baseName));
        }

        return typeInfo;
    }

    /// <summary>
    /// The class this node presents as.
    /// </summary>
    /// <remarks>
    /// The promoted node is a second RichTextLabel that carries NO text. That is deliberate: it makes
    /// the class set big enough for the subset rule to constrain anything at all, and it exercises
    /// the subset itself — one instance decodes, the other does not, and the offset must still
    /// publish. The authored grid scene has exactly one RichTextLabel, so without this the rule is
    /// vacuous for that class and the tests would be measuring the scene's arithmetic.
    /// </remarks>
    private string ClassOf(GridNode node)
        => _secondRich && node.Name == "OmegaChild" ? "RichTextLabel" : node.Class;

    /// <summary>A Label-shaped text field at an arbitrary offset, for building decoys.</summary>
    private void WriteLabelAt(ulong node, int offset, string text)
    {
        ulong buffer = WriteCowData(node + (ulong)offset, text);
        WriteUInt64(node + (ulong)offset - 8, 0);

        // xl_text normally SHARES text's allocation, measured bit-identical in target memory on both
        // bindings. Pointing it at an equal-but-separate buffer is what a translated build would look
        // like, and the bracket is entitled to decline that rather than guess.
        if (_unsharedXlText)
        {
            WriteCowData(node + (ulong)offset + 8, text);
        }
        else
        {
            WritePointer(node + (ulong)offset + 8, buffer);
        }

        WriteUInt64(buffer - 16, 2);
        WriteUInt32(node + (ulong)offset + 16, 0);   // autowrap_mode

        // A live member past xl_text, NOT reserved padding: measured varying between two Labels in one
        // process. The fixture must not present it as zero, or it would confirm a false clause.
        WriteUInt32(node + (ulong)offset + 20, 0x5a);
    }

    /// <summary>Paths of the two <c>Label</c> nodes.</summary>
    private static readonly string[] LabelPaths =
    [
        "RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelAscii",
        "RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelUnicode",
    ];

    /// <summary>
    /// Per-frame state and short-lived allocations, all of which look exactly like the real thing in
    /// a single reading.
    /// </summary>
    private void WriteTransients()
    {
        // 1. A flawless `visible` impostor: boolean on every node, 1 on the visible twin and 0 on
        //    the hidden one. Nothing in a single reading tells it apart from the real flag.
        foreach (GridNode node in Nodes)
        {
            Write(ByPath[node.Path] + 0x2e0, [node.Visible ? (byte)1 : (byte)0]);
        }

        ulong twin = ByPath["RootHarness/AlphaPanel/BetaBranch/VisibleTwin"];
        _frameAdvance.Add(() => Write(twin + 0x2e0, [0]));

        // 1b. notify_local_transform at V+8: the ONE decoy that is also a multiple of eight, so the
        //     alignment rule cannot touch it. Only the qword behind it separates the two — eight
        //     bools where `visible` has a Window pointer.
        Write(twin + (ulong)CanvasItemVisible + 8, [1]);
        Write(ByPath["RootHarness/AlphaPanel/BetaBranch/HiddenTwin"] + (ulong)CanvasItemVisible + 8, [0]);

        // 2. A shaped-text cache on both Labels: varies per node, so it is a text candidate, and it
        //    disagrees with the authored text. Rebuilt as the engine runs.
        uint rebuild = 'A';
        foreach (string path in LabelPaths)
        {
            ulong buffer = WriteCowData(ByPath[path] + 0x8c0, "cached:" + path[(path.LastIndexOf('/') + 1)..]);

            // Rebuilt on EVERY frame, not settled after the first: a cache that stopped changing
            // would be indistinguishable from the authored text by this test, and saying so is the
            // honest limit of a two-reading check.
            _frameAdvance.Add(() =>
            {
                Span<byte> unit = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(unit, rebuild++);
                Write(buffer, unit);
            });
        }

        // 3. A short-lived allocation that happens to point back at the root, which is all pointer
        //    identity asks of a ScriptInstance when only the root is scripted.
        ulong ephemeral = Allocate(0x40);
        WritePointer(Root + 0x88, ephemeral);
        WritePointer(ephemeral + 0x10, Root);
        _frameAdvance.Add(() => WritePointer(Root + 0x88, 0));
    }

    private void WriteDecoys()
    {
        // §12.5 probe 15: one control's size scan returned 0x4c0, 0x4c8, 0x4d4 and 0x4f4.
        ulong alpha = ByPath["RootHarness/AlphaPanel"];
        WriteReals(alpha + 0x4c8, [613, 227]);
        WriteReals(alpha + 0x4d4, [613, 227]);
        WriteReals(alpha + 0x4f4, [613, 227]);

        // A byte that separates the twins but is not boolean elsewhere: raw difference is not
        // evidence, so the visible derivation must reject this.
        Write(ByPath["RootHarness/AlphaPanel/BetaBranch/VisibleTwin"] + 0x2f0, [1]);
        Write(ByPath["RootHarness/AlphaPanel/BetaBranch/HiddenTwin"] + 0x2f0, [0]);
    }

    private ulong Allocate(int size)
    {
        ulong address;
        if (size >= NodeBlock)
        {
            address = _nextNode;
            _nextNode += (ulong)size + 0x1000;
        }
        else
        {
            address = _nextAux;
            _nextAux += (ulong)size + 0x40;
        }

        byte[] bytes = new byte[size];
        Array.Fill(bytes, Junk);
        _blocks.Add((address, bytes));
        return address;
    }

    private void Write(ulong address, ReadOnlySpan<byte> data)
    {
        foreach ((ulong start, byte[] bytes) in _blocks)
        {
            if (address >= start && address + (ulong)data.Length <= start + (ulong)bytes.Length)
            {
                data.CopyTo(bytes.AsSpan((int)(address - start)));
                return;
            }
        }

        throw new InvalidOperationException($"nothing mapped at 0x{address:x}");
    }

    private void WritePointer(ulong address, ulong value) => WriteUInt64(address, value);

    private void WriteUInt64(ulong address, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        Write(address, buffer);
    }

    private void WriteUInt32(ulong address, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        Write(address, buffer);
    }

    private void WriteReals(ulong address, IReadOnlyList<double> values)
    {
        Span<byte> buffer = stackalloc byte[4];
        for (int i = 0; i < values.Count; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(buffer, (float)values[i]);
            Write(address + (ulong)(i * 4), buffer);
        }
    }

    private void WriteName(ulong node, string name)
    {
        ulong data = Allocate(0x40);
        WritePointer(node + NodeName, data);
        NameBuffers[name] = WriteCowData(data + 8, name);
    }

    private void WriteString(ulong field, string text)
    {
        WriteCowData(field, text);
    }

    /// <summary>Writes a <c>CowData&lt;char32_t&gt;</c>: <c>[refcount][size]</c> ahead of the buffer.</summary>
    private ulong WriteCowData(ulong pointerField, string value)
    {
        ulong block = Allocate(0x400);
        ulong buffer = block + 0x40;

        List<uint> units = [];
        foreach (Rune rune in value.EnumerateRunes())
        {
            units.Add((uint)rune.Value);
        }

        units.Add(0);

        // The full header, not just the size: CowData stores [refcount][size] ahead of the data and
        // points at the data (§4.6). A calibrator is entitled to use the refcount to tell a real
        // buffer from any pointer-shaped eight bytes, so the fixture has to lay one down.
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(header, 1);
        Write(buffer - 16, header);

        BinaryPrimitives.WriteUInt64LittleEndian(header, (ulong)units.Count);
        Write(buffer - 8, header);

        Span<byte> unit = stackalloc byte[4];
        for (int i = 0; i < units.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(unit, units[i]);
            Write(buffer + (ulong)(i * 4), unit);
        }

        WritePointer(pointerField, buffer);
        return buffer;
    }
}
