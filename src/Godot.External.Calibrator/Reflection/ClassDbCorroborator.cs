using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using Godot.External.Abi;
using Godot.External.Calibrator.Interop;
using Godot.External.Calibrator.Protocol;
using Godot.External.Calibrator.Target;
using Godot.External.Reflection;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Reflection;

/// <summary>
/// The second, unrelated route to the same offsets, run live against the same process in the same
/// pass as the bracketed answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is worth its cost.</b> The calibrator publishes an offset when exactly one candidate
/// survives bracketing, which is a statement about a <em>search space</em>. Reading the property
/// getter's machine code is a statement about the <em>engine</em>, and it never looks at the heap.
/// The two share no inputs, no assumptions and no failure modes, so two agreeing derivations are
/// worth far more than either one narrowing to a single survivor (docs/analysis.md §13.6).
/// </para>
/// <para>
/// <b>What it deliberately is not.</b> It is not a table. §13.11's corollary is that an offset column
/// transcribed from the calibrator's own output makes agreement pass by construction and keeps
/// passing forever, so nothing here is written to <c>profiles.json</c> or
/// <c>GodotAbiProfiles</c>. The corroboration lives in the comparison.
/// </para>
/// <para>
/// <b>The chain, and where each link can fail honestly:</b>
/// </para>
/// <list type="number">
/// <item><see cref="GodotReflectionSupport"/> gates the version and platform, or the whole route reports
/// <c>unsupported</c>.</item>
/// <item><see cref="ClassDbSeed"/> identifies one element of <c>ClassDB::classes</c> by content, or the
/// route reports <c>noSeed</c>.</item>
/// <item><see cref="ClassDbElementWalk"/> enumerates every registered class from it — 908 on 4.5, 869
/// on 4.3, in under a millisecond.</item>
/// <item><see cref="MethodBindResolver"/> finds the class's <em>own</em> named bind, or there is no
/// name and the field reports <c>notCompared</c>.</item>
/// <item><see cref="MethodBindProbe"/> recovers the typed method pointer, and
/// <see cref="GetterFieldDecoder"/> decodes the body — or abstains, which is the correct and common
/// answer for a computed getter and for every field on a debug template.</item>
/// </list>
/// <para>
/// <b>Debug templates abstain structurally, and that is not a defect to work around.</b> Unoptimized
/// codegen spills <c>this</c> to the stack and reloads it into a register the decoder does not track,
/// so it reports <c>NoThisRelativeAccess</c>. Widening the window does not help; stack-slot tracking
/// would, and is not implemented (§16.2).
/// </para>
/// </remarks>
public sealed class ClassDbCorroborator
{
    /// <summary>Bytes of getter body read for decoding. Matches <see cref="GetterDecoderOptions.WindowBytes"/>.</summary>
    public const int GetterBodyBytes = 0x40;

    /// <summary>
    /// The fields with a plain accessor to disassemble, and whose accessor it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every entry names a method the class <b>binds itself</b>. That is not a stylistic choice:
    /// <c>ClassInfo::method_map</c> holds only the class's own registrations, so
    /// <c>Control::get_text</c> resolves to nothing even though a <c>Control</c> answers it, and an
    /// entry naming an inherited method would silently corroborate nothing on every target.
    /// </para>
    /// <para>
    /// Keys the calibrator derives but that have <em>no</em> such accessor —
    /// <c>node.childListHead</c>, <c>node.scriptInstance</c>, <c>control.offset</c>,
    /// <c>control.globalPosition</c> — are simply absent. Their absence from the record list is the
    /// honest report; inventing a probe for them would produce a refusal that reads like coverage.
    /// </para>
    /// <para>
    /// <c>node.name</c> is included even though <c>Node::get_name</c> abstains on every measured
    /// target (it returns a member of a nested struct through a shape the decoder will not attribute).
    /// A route that only probed the fields it already knew decode is the §13.11 shape again — the
    /// abstention is a result, and it is published as one.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string Key, string Class, string Method)> DefaultProbes { get; } =
    [
        ("canvasItem.visible", "CanvasItem", "is_visible"),
        ("control.size", "Control", "get_size"),
        ("control.position", "Control", "get_position"),
        ("control.scale", "Control", "get_scale"),
        ("node.parent", "Node", "get_parent"),
        ("node.name", "Node", "get_name"),
        ("label.text", "Label", "get_text"),
        ("richTextLabel.text", "RichTextLabel", "get_text"),
    ];

    private readonly IByteSource _source;
    private readonly RegionScanner _scanner;
    private readonly ulong _moduleBase;
    private readonly int _major;
    private readonly int _minor;
    private readonly IReadOnlyList<(string Key, string Class, string Method)> _probes;

    /// <summary>Creates a corroborator over one live target.</summary>
    /// <param name="reader">Target memory.</param>
    /// <param name="scanner">Whole-process scanner; the seed needs one and the library has none.</param>
    /// <param name="mainModuleBase">Base address of the engine executable.</param>
    /// <param name="engineVersion">The engine's own version string, e.g. <c>4.5.stable</c>.</param>
    /// <param name="probes">Override for <see cref="DefaultProbes"/>; used by tests.</param>
    public ClassDbCorroborator(
        IMemoryReader reader,
        RegionScanner scanner,
        ulong mainModuleBase,
        string? engineVersion,
        IReadOnlyList<(string Key, string Class, string Method)>? probes = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(scanner);

        _source = new MemoryReaderByteSource(reader);
        _scanner = scanner;
        _moduleBase = mainModuleBase;
        _probes = probes ?? DefaultProbes;
        (_major, _minor) = ParseEngineVersion(engineVersion);
    }

    /// <summary>
    /// Runs the whole route against <paramref name="derived"/> — the offsets this same calibration
    /// bracketed — and returns one verdict per probed field.
    /// </summary>
    /// <param name="derived">
    /// The independently derived offsets. A key absent here is not an error: the getter route still
    /// runs and reports <c>noOpinion</c>, because "one route published and the other did not" is a
    /// different fact from "the two disagreed".
    /// </param>
    public GetterCorroboration Corroborate(IReadOnlyDictionary<string, int> derived)
    {
        ArgumentNullException.ThrowIfNull(derived);

        Stopwatch clock = Stopwatch.StartNew();

        if (!GodotReflectionSupport.TryResolve(_major, _minor, isWindows: true, out ClassDbLayout layout, out string gate))
        {
            return new GetterCorroboration
            {
                Status = CorroborationStatuses.Unsupported,
                Reason = gate,
                ElapsedMilliseconds = clock.ElapsedMilliseconds,
            };
        }

        if (_moduleBase == 0 || !TryFindTextSection(out CodeRegion text))
        {
            return new GetterCorroboration
            {
                Status = CorroborationStatuses.Unsupported,
                Reason = "the engine executable's .text section could not be located, so a probed qword "
                       + "cannot be told from a heap pointer and no getter may be trusted",
                ElapsedMilliseconds = clock.ElapsedMilliseconds,
            };
        }

        MethodBindResolver binds = new(_source, layout);

        if (!ClassDbSeed.TrySeed(_source, _scanner, layout, binds, out ClassDbSeedResult? seed, out string why)
            || seed is null)
        {
            return new GetterCorroboration
            {
                Status = CorroborationStatuses.NoSeed,
                Reason = why,
                ElapsedMilliseconds = clock.ElapsedMilliseconds,
            };
        }

        InternedNameReader names = new(_source, seed.NameOffset, seed.CompileTimeNameOffset, layout.CowDataSizeBackOffset);

        ClassDbElementWalk.TryEnumerate(_source, seed.Element, layout, out IReadOnlyList<ulong> elements, out _);

        Dictionary<string, ulong> classes = new(StringComparer.Ordinal);
        foreach (ulong element in elements)
        {
            if (ClassDbElementWalk.TryReadKeyPointer(_source, element, layout, out ulong key)
                && names.TryRead(key, out string name)
                && name.Length > 0)
            {
                classes.TryAdd(name, element);
            }
        }

        List<CorroborationRecord> records = [];
        foreach ((string key, string className, string method) in _probes)
        {
            records.Add(Probe(layout, binds, names, classes, text, key, className, method, derived));
        }

        return new GetterCorroboration
        {
            Status = CorroborationStatuses.Ran,
            Seed = new CorroborationSeed
            {
                SeedClass = seed.SeedClass,
                Method = seed.SeedMethod,
                Element = Wire.Pointer(seed.Element),
                Candidates = seed.StructuralCandidates,
                Identified = seed.IdentifiedCandidates,
                Classes = classes.Count,
                DataNameOffset = Wire.Offset(seed.NameOffset),
                Evidence = seed.Evidence,
            },
            Records = records,
            ElapsedMilliseconds = clock.ElapsedMilliseconds,
        };
    }

    private CorroborationRecord Probe(
        ClassDbLayout layout,
        MethodBindResolver binds,
        InternedNameReader names,
        IReadOnlyDictionary<string, ulong> classes,
        CodeRegion text,
        string key,
        string className,
        string method,
        IReadOnlyDictionary<string, int> derived)
    {
        int? independent = derived.TryGetValue(key, out int value) ? value : null;

        // Every failure below reaches the SAME call — Compare with no attribution — so a field that
        // could not be named cannot take a different path to a verdict than one that could. That is
        // what stops "not compared" from quietly becoming "agree" at some later refactor.
        if (!classes.TryGetValue(className, out ulong element))
        {
            return NotCompared(key, $"{className} is not in the {classes.Count} classes walked from the seed");
        }

        ulong classInfo = element + (ulong)layout.ElementData + ByteSourceExtensions.PointerWidth;
        if (!binds.TryResolve(classInfo, className, method, names, out ulong bind, out string bindEvidence))
        {
            return NotCompared(key, bindEvidence);
        }

        if (!MethodBindProbe.TryFindMethodPointer(_source, bind, text, out ulong code, out int slot, out string probeReason))
        {
            return NotCompared(
                key,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}::{1} resolved to MethodBind 0x{2:x} but its typed method pointer could not be recovered: {3}",
                    className,
                    method,
                    bind,
                    probeReason));
        }

        byte[] body = new byte[GetterBodyBytes];
        if (!_source.TryRead(code, body))
        {
            return NotCompared(
                key,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}::{1}'s getter at 0x{2:x} is unreadable",
                    className,
                    method,
                    code));
        }

        GetterAttribution attribution = new()
        {
            ClassName = className,
            MethodName = method,
            MethodBindAddress = bind,
            CodeAddress = code,
            CodeRva = code - _moduleBase,
            Evidence = string.Format(
                CultureInfo.InvariantCulture,
                "{0}; method pointer at MethodBind slot {1}, i.e. sizeof(MethodBind)=0x{2:x}",
                bindEvidence,
                slot,
                slot * ByteSourceExtensions.PointerWidth),
        };

        FieldOffsetDecodeResult decoded = GetterFieldDecoder.Decode(body, null, code);
        OffsetCrossCheckResult verdict = OffsetCrossCheck.Compare(decoded, independent, attribution);

        return new CorroborationRecord
        {
            Key = key,
            Agreement = Spell(verdict.Agreement),
            RecordClass = className,
            RecordMethod = method,
            GetterRva = Wire.Pointer(attribution.CodeRva),
            MethodBind = Wire.Pointer(bind),

            // The only place a value is written, and only for Agree. OffsetCrossCheckResult already
            // nulls its own Offset on Disagree and NoOpinion; the redundant guard is here because a
            // published number is the one thing this whole exercise must not be able to leak.
            Offset = verdict.Agreement == OffsetAgreement.Agree && verdict.Offset is { } corroborated
                ? Wire.Offset(corroborated)
                : null,
            Reason = verdict.Reason,
        };
    }

    /// <summary>
    /// The single construction site for "no name, therefore no comparison" — routed through
    /// <see cref="OffsetCrossCheck"/> rather than assembled by hand, so the rule lives in one place.
    /// </summary>
    private static CorroborationRecord NotCompared(string key, string detail)
    {
        OffsetCrossCheckResult verdict = OffsetCrossCheck.Compare(
            GetterFieldDecoder.Decode([]),
            null,
            attribution: null);

        return new CorroborationRecord
        {
            Key = key,
            Agreement = Spell(verdict.Agreement),
            Reason = detail + " — " + verdict.Reason,
        };
    }

    private static string Spell(OffsetAgreement agreement) => agreement switch
    {
        OffsetAgreement.Agree => CorroborationVerdicts.Agree,
        OffsetAgreement.Disagree => CorroborationVerdicts.Disagree,
        OffsetAgreement.NoOpinion => CorroborationVerdicts.NoOpinion,
        _ => CorroborationVerdicts.NotCompared,
    };

    /// <summary>
    /// Parses the engine's own version string into (major, minor).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shape varies and the variation is not cosmetic.</b> The grid's targets report
    /// <c>4.5-stable (official)</c>, while <c>Engine::get_version_info()</c> and every doc example
    /// spell it <c>4.4.1.stable.mono</c>. So only the leading digit run of each of the first two
    /// dot- or dash-separated parts is read; a stricter parse silently produced <c>(0, 0)</c> on the
    /// live grid, which then reported the whole route as "Godot 0.x is unsupported" — a refusal that
    /// looks exactly like a deliberate version gate and is in fact a string bug.
    /// </para>
    /// <para>
    /// An unparseable version still yields <c>(0, 0)</c> and <see cref="GodotReflectionSupport"/>
    /// still refuses it. Guessing would send a 4.6 target down the 4.5 walker, whose only symptom is
    /// that every name reads empty.
    /// </para>
    /// </remarks>
    internal static (int Major, int Minor) ParseEngineVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return (0, 0);
        }

        string[] parts = version.Split(['.', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && TryLeadingNumber(parts[0], out int major)
            && TryLeadingNumber(parts[1], out int minor)
            ? (major, minor)
            : (0, 0);
    }

    private static bool TryLeadingNumber(string text, out int value)
    {
        int length = 0;
        while (length < text.Length && char.IsAsciiDigit(text[length]))
        {
            length++;
        }

        value = 0;
        return length > 0
            && int.TryParse(text.AsSpan(0, length), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Reads the target's own PE headers to find the executable section.
    /// </summary>
    /// <remarks>
    /// Read out of the target rather than off disk, for the same reason the rest of this project does:
    /// the mapped image is the thing being decoded, and a local copy is a different file until proven
    /// otherwise.
    /// </remarks>
    private bool TryFindTextSection(out CodeRegion text)
    {
        text = default;
        byte[] header = new byte[0x1000];

        if (!_source.TryRead(_moduleBase, header) || BinaryPrimitives.ReadUInt16LittleEndian(header) != 0x5a4d)
        {
            return false;
        }

        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x3c));
        if (peOffset <= 0 || peOffset + 24 > header.Length)
        {
            return false;
        }

        int sections = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(peOffset + 6));
        int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(peOffset + 20));
        int table = peOffset + 24 + optionalSize;

        for (int i = 0; i < sections; i++)
        {
            int entry = table + (i * 40);
            if (entry + 40 > header.Length)
            {
                return false;
            }

            const uint ImageScnMemExecute = 0x2000_0000;
            if ((BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(entry + 36)) & ImageScnMemExecute) == 0)
            {
                continue;
            }

            uint virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(entry + 8));
            uint rva = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(entry + 12));
            if (virtualSize == 0)
            {
                continue;
            }

            text = new CodeRegion(_moduleBase + rva, _moduleBase + rva + virtualSize);
            return true;
        }

        return false;
    }
}
