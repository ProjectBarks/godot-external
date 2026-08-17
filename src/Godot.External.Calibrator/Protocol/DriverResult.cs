using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Godot.External.Calibrator.Protocol;

/// <summary>How offsets and pointers are rendered on the wire.</summary>
/// <remarks>
/// tools/godot-abi-grid/README.md: "Offsets may be hex strings or integers. Pointers <b>must</b> be
/// strings — a 64-bit pointer through a JS Number is a silent corruption, and the harness rejects
/// unsafe integers rather than guessing." Both are emitted as <c>0x…</c> strings here so there is
/// one rule rather than two.
/// </remarks>
public static class Wire
{
    /// <summary>Renders a byte offset as <c>0x148</c>.</summary>
    public static string Offset(int value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);

    /// <summary>Renders a native pointer as <c>0x1a9204c5580</c>.</summary>
    public static string Pointer(ulong value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);
}

/// <summary>One node as the calibrator read it, using only offsets it derived.</summary>
public sealed record NodeRecord
{
    /// <summary>Decoded <c>StringName</c>.</summary>
    public string? Name { get; init; }

    /// <summary>The node's class. See <see cref="ClassSource"/> for where the answer came from.</summary>
    [JsonPropertyName("class")]
    public string? NodeClass { get; init; }

    /// <summary>
    /// How <see cref="NodeClass"/> was arrived at: <c>engine</c> when read from
    /// <c>Object::_class_name_ptr</c>, <c>geometry-guess</c> when merely inferred from whether the
    /// node's bytes read as Control geometry.
    /// </summary>
    /// <remarks>
    /// These are not the same claim and must not share a field without saying so. The guess reports
    /// "Control" for a ColorRect, a Panel, a Label and a RichTextLabel alike — it did so for 108
    /// nodes in one series — and a reader with no way to tell the two apart is entitled to believe
    /// the engine said it.
    /// </remarks>
    public string? ClassSource { get; init; }

    /// <summary>The engine-side <c>Node*</c>.</summary>
    public string NativePtr { get; init; } = "0x0";

    /// <summary>Parent <c>Node*</c>, null for the walk root.</summary>
    public string? ParentPtr { get; init; }

    /// <summary>Children in child-list order.</summary>
    public IReadOnlyList<string> ChildPtrs { get; init; } = [];

    /// <summary><c>Control::Data::size_cache</c>.</summary>
    public double[]? Size { get; init; }

    /// <summary><c>Control::Data::pos_cache</c>.</summary>
    public double[]? Position { get; init; }

    /// <summary><c>Control::Data::scale</c>.</summary>
    public double[]? Scale { get; init; }

    /// <summary><c>Control::Data::offset[4]</c> — the raw offsets, not the resolved rect.</summary>
    public double[]? Offset { get; init; }

    /// <summary><c>Control::Data::anchor[4]</c>, when it was separated from <see cref="Offset"/>.</summary>
    public double[]? Anchors { get; init; }

    /// <summary><c>CanvasItem::visible</c>.</summary>
    public bool? Visible { get; init; }

    /// <summary>Decoded text for Label/RichTextLabel nodes.</summary>
    public string? Text { get; init; }
}

/// <summary>Offsets derived by pointer identity, plus how each one was pinned down.</summary>
public sealed record StructuralDerivation
{
    /// <summary>Always <c>pointer-identity</c> when anything is reported at all.</summary>
    public string Method { get; init; } = DerivationMethods.Structural;

    /// <summary>Contract-keyed offsets.</summary>
    public IReadOnlyDictionary<string, string> Offsets { get; init; } = new Dictionary<string, string>();

    /// <summary>One sentence per offset naming the identity that fixed it.</summary>
    public IReadOnlyDictionary<string, string> Evidence { get; init; } = new Dictionary<string, string>();
}

/// <summary>Offsets derived by intersecting known-value scans across several objects.</summary>
public sealed record SemanticDerivation
{
    /// <summary>Always <c>known-value-intersection</c>.</summary>
    public string Method { get; init; } = DerivationMethods.Semantic;

    /// <summary>Contract-keyed offsets. A key is absent when the intersection did not resolve.</summary>
    public IReadOnlyDictionary<string, string> Offsets { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Distinct objects that contributed to each offset. Per key, because the samples genuinely
    /// differ per field and one global number would be a claim the calibrator cannot make.
    /// </summary>
    public IReadOnlyDictionary<string, int> Samples { get; init; } = new Dictionary<string, int>();

    /// <summary>Every surviving candidate, so an ambiguous derivation is visible rather than hidden.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Candidates { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>String field offsets.</summary>
public sealed record StringsDerivation
{
    /// <summary>The decode mechanism: one bulk read of <c>len*4</c> bytes, then UTF-32 (§4.6).</summary>
    public string Method { get; init; } = DerivationMethods.Strings;

    /// <summary>Contract-keyed offsets.</summary>
    public IReadOnlyDictionary<string, string> Offsets { get; init; } = new Dictionary<string, string>();

    /// <summary>Evidence per offset.</summary>
    public IReadOnlyDictionary<string, string> Evidence { get; init; } = new Dictionary<string, string>();
}

/// <summary>Intrusive-list and ScriptInstance link offsets.</summary>
public sealed record WalkDerivation
{
    /// <summary>Contract-keyed offsets.</summary>
    public IReadOnlyDictionary<string, string> Offsets { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The C++ class implementing the <c>ScriptInstance</c> the walk root carries — read from its
    /// vtable, not inferred from the cell.
    /// </summary>
    /// <remarks>
    /// <c>scriptInstance.ownerBackref</c> is a member of <em>this</em> class, not of the engine
    /// object, so it is the only offset here whose correct value depends on something other than the
    /// build. <c>CSharpInstance</c> and <c>GDScriptInstance</c> are unrelated implementations of one
    /// interface and put the owner pointer in different places — measured <c>0x8</c> and <c>0x10</c>.
    /// Reporting the class is what lets a cross-check resolve the right expected value instead of
    /// guessing from the cell's binding, which is not the same question.
    /// </remarks>
    public string? ScriptInstanceClass { get; init; }
}

/// <summary>The four derivation groups the harness scores separately.</summary>
public sealed record Derivation
{
    /// <summary>(a) pointer identity.</summary>
    public StructuralDerivation Structural { get; init; } = new();

    /// <summary>(b) known-value intersection.</summary>
    public SemanticDerivation Semantic { get; init; } = new();

    /// <summary>(c) strings.</summary>
    public StringsDerivation Strings { get; init; } = new();

    /// <summary>Link offsets used by the walk.</summary>
    public WalkDerivation Walk { get; init; } = new();
}

/// <summary>The §4.6 native → managed chain, when it was followed.</summary>
public sealed record ReverseChain
{
    /// <summary><c>ScriptInstance + ownerBackref</c>, which must be the node again.</summary>
    public string? OwnerBackref { get; init; }

    /// <summary><c>ScriptInstance + gcHandle</c>.</summary>
    public string? GcHandle { get; init; }
}

/// <summary>Managed static root → <c>NativePtr</c> → the native walk root.</summary>
public sealed record ManagedBridgeResult
{
    /// <summary>Managed type the root resolved to.</summary>
    public string? StaticRootType { get; init; }

    /// <summary>Static field the root resolved through.</summary>
    public string? StaticRootField { get; init; }

    /// <summary>The <c>NativePtr</c> the managed object carries.</summary>
    public string? NativePtr { get; init; }

    /// <summary>The native → managed direction, for the free self-check on <c>+0x08</c>.</summary>
    public ReverseChain? Reverse { get; init; }

    /// <summary>Managed instance fields read through the bridge.</summary>
    public IReadOnlyDictionary<string, object?>? Fields { get; init; }
}

/// <summary>
/// The single JSON object this driver writes to stdout.
/// </summary>
/// <remarks>
/// <see cref="UsedProfile"/> is a constant <see langword="false"/> and there is deliberately no way
/// to set it: §8.9's <c>calibration.unaided</c> is the check the whole grid exists to make, and a
/// calibrator that can flip that flag from its own configuration is one refactor away from
/// answering out of <c>GodotAbiProfiles</c>. Profile comparison happens in
/// <see cref="ProfileCrossCheck"/>, which is written <em>after</em> derivation and which the harness
/// ignores.
/// </remarks>
public sealed record DriverResult
{
    /// <summary>Driver identity.</summary>
    public string Driver { get; init; } = "godot-external";

    /// <summary>Driver version.</summary>
    public string DriverVersion { get; init; } = "0.1.0";

    /// <summary>Never true. See the remarks on this type.</summary>
    public bool UsedProfile => false;

    /// <summary>The engine's own version string, echoed from the ready file.</summary>
    public string? EngineVersion { get; init; }

    /// <summary>Nodes reached by the derived walk.</summary>
    public int WalkCount { get; init; }

    /// <summary>The four derivation groups.</summary>
    public Derivation Derivation { get; init; } = new();

    /// <summary>Per-node readings.</summary>
    public IReadOnlyList<NodeRecord> Nodes { get; init; } = [];

    /// <summary>Managed bridge result, or null on a GDScript cell.</summary>
    public ManagedBridgeResult? ManagedBridge { get; init; }

    /// <summary>
    /// Agreement or divergence against the shipped table, computed after every offset was derived.
    /// Unknown to the harness schema on purpose — it must never look like an input.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ProfileCrossCheck { get; init; }

    /// <summary>Human-readable notes; the harness merges stderr into the same list.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,

        // A backstop, not the fix. Readings are sanitised before they reach a NodeRecord, but the
        // serializer throws on Infinity and NaN, and a throw here loses the WHOLE cell — every
        // derived offset and every other node — to one garbage float. No numeric value may ever be
        // able to do that.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>Serialises to the wire form, opening brace first.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

/// <summary>The method strings <c>lib/checks.mjs</c> compares against, exactly.</summary>
public static class DerivationMethods
{
    /// <summary>§8.9 (a).</summary>
    public const string Structural = "pointer-identity";

    /// <summary>§8.9 (b).</summary>
    public const string Semantic = "known-value-intersection";

    /// <summary>§4.6's clean <c>getText</c> path: one bulk read, then UTF-32.</summary>
    public const string Strings = "cowdata-bulk-utf32";
}
