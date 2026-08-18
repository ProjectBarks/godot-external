namespace Godot.External.Calibrator.Protocol;

/// <summary>
/// The <c>ClassDB</c> element the whole route hangs off, and how it was identified.
/// </summary>
/// <remarks>
/// Published so a reader can see what the corroboration <em>rests on</em>. A verdict computed from a
/// wrongly identified seed is not a weaker verdict, it is a verdict about a different container —
/// which is exactly the failure "pick the longest chain" produced by selecting GDScript's
/// <c>global_map</c> (docs/analysis.md §16.1).
/// </remarks>
public sealed record CorroborationSeed
{
    /// <summary>Class name the seed searched for and read back off the element's own key.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("class")]
    public string? SeedClass { get; init; }

    /// <summary>The class's own method whose <c>MethodBind</c> identified the element.</summary>
    public string? Method { get; init; }

    /// <summary>Address of the identified <c>HashMapElement</c>.</summary>
    public string? Element { get; init; }

    /// <summary>Structurally valid candidates the scan produced.</summary>
    public int Candidates { get; init; }

    /// <summary>How many of those identified. Anything but 1 publishes no seed at all.</summary>
    public int Identified { get; init; }

    /// <summary>Classes enumerated from the seed by walking the intrusive chain.</summary>
    public int Classes { get; init; }

    /// <summary>Measured offset of <c>StringName::_Data::name</c>, as <c>0x8</c>.</summary>
    public string? DataNameOffset { get; init; }

    /// <summary>One line naming every gate the surviving candidate passed.</summary>
    public string? Evidence { get; init; }
}

/// <summary>
/// One field's verdict: what the getter route said, whose getter said it, and whether that agreed
/// with the offset this same run derived by bracketing.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Offset"/> is populated only when <see cref="Agreement"/> is <c>agree</c>.</b> Not on
/// a disagreement — a disagreement falsifies one of the two derivations without saying which, so
/// publishing either would be picking a winner by fiat — and not on a refusal. There is deliberately
/// no field carrying "the value we would have published", because a reader who can see the number
/// will use it.
/// </para>
/// <para>
/// <see cref="RecordClass"/> and <see cref="RecordMethod"/> are the point of the record. An
/// <c>agree</c> without them is the §13.2 defect in its published form: a decoded offset that matched
/// the answer, from a function nobody identified.
/// </para>
/// </remarks>
public sealed record CorroborationRecord
{
    /// <summary>Contract key, e.g. <c>canvasItem.visible</c>.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// <c>agree</c>, <c>disagree</c>, <c>noOpinion</c> or <c>notCompared</c>, spelled exactly as
    /// <see cref="CorroborationVerdicts"/> spells them.
    /// </summary>
    public string Agreement { get; init; } = CorroborationVerdicts.NotCompared;

    /// <summary>Class the decoded getter was proved to belong to. Null when it was never named.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("class")]
    public string? RecordClass { get; init; }

    /// <summary>Method the decoded getter was proved to be. Null when it was never named.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("method")]
    public string? RecordMethod { get; init; }

    /// <summary>The getter's RVA in the main module — the form docs/analysis.md quotes.</summary>
    public string? GetterRva { get; init; }

    /// <summary>The <c>MethodBind</c> the name resolved to.</summary>
    public string? MethodBind { get; init; }

    /// <summary>The corroborated offset. Present <b>only</b> on <c>agree</c>.</summary>
    public string? Offset { get; init; }

    /// <summary>Why, in words. Always populated.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>The four verdict spellings, shared by the driver and <c>lib/checks.mjs</c>.</summary>
public static class CorroborationVerdicts
{
    /// <summary>Two independent derivations published and matched.</summary>
    public const string Agree = "agree";

    /// <summary>Both published and differ. Neither is published onward.</summary>
    public const string Disagree = "disagree";

    /// <summary>One side abstained — a refused decode, or no bracketed value to compare.</summary>
    public const string NoOpinion = "noOpinion";

    /// <summary>No named getter, so no comparison was attempted at all.</summary>
    public const string NotCompared = "notCompared";
}

/// <summary>
/// The whole live-corroboration section of a driver result: an independent route to the same offsets,
/// computed in the same run as the bracketed answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is published beside the derivation, never instead of it.</b> The calibrator's own
/// candidate lists, samples and evidence are untouched by anything here — a caller reading
/// <c>derivation</c> sees exactly what it saw before. Corroboration is a second opinion, and folding
/// it into the first would destroy the only property that makes it worth having.
/// </para>
/// <para>
/// <b>And it must never become a table.</b> docs/analysis.md §13.11's corollary: an offset column
/// transcribed from the calibrator's own output makes agreement pass by construction, forever. The
/// numbers here are computed against the target in front of us, from machine code the calibrator
/// never reads, and nothing writes them to disk as a profile.
/// </para>
/// </remarks>
public sealed record GetterCorroboration
{
    /// <summary>The route, exactly as <c>lib/checks.mjs</c> compares it.</summary>
    public string Method { get; init; } = CorroborationMethods.ClassDbGetter;

    /// <summary><c>ran</c>, <c>unsupported</c> or <c>noSeed</c>.</summary>
    public string Status { get; init; } = CorroborationStatuses.Unsupported;

    /// <summary>Why the route did not run. Empty when it did.</summary>
    public string? Reason { get; init; }

    /// <summary>The identified <c>ClassDB</c> element. Null unless <see cref="Status"/> is <c>ran</c>.</summary>
    public CorroborationSeed? Seed { get; init; }

    /// <summary>One verdict per probed field.</summary>
    public IReadOnlyList<CorroborationRecord> Records { get; init; } = [];

    /// <summary>How long the whole route took, so its cost stays visible rather than assumed.</summary>
    public long ElapsedMilliseconds { get; init; }
}

/// <summary>Route names, matched exactly by the harness.</summary>
public static class CorroborationMethods
{
    /// <summary>Seed <c>ClassDB::classes</c>, resolve a bind by name, disassemble its getter.</summary>
    public const string ClassDbGetter = "classdb-getter-disassembly";
}

/// <summary>Whether the route ran, and if not, why not.</summary>
public static class CorroborationStatuses
{
    /// <summary>The seed was found and every probe was attempted.</summary>
    public const string Ran = "ran";

    /// <summary>The engine version or platform is outside what this route was checked against.</summary>
    public const string Unsupported = "unsupported";

    /// <summary>Supported, but no <c>ClassDB</c> element could be uniquely identified.</summary>
    public const string NoSeed = "noSeed";
}
