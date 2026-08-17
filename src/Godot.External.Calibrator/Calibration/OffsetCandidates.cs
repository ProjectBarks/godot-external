using LiveClr.Calibration;

namespace Godot.External.Calibrator.Calibration;

/// <summary>
/// A set of surviving candidate offsets, with everything needed to decide whether it is an answer.
/// </summary>
/// <remarks>
/// <para>
/// The same contract as LiveClr's <see cref="CalibrationResult"/> — and results produced by
/// <see cref="LiveClr.Calibration.StructuralProbe"/> convert straight into it via
/// <see cref="From"/>. It exists separately only because the Godot predicates
/// this calibrator needs (a four-<c>real_t</c> window whose <em>differences</em> equal a known size;
/// a byte that is boolean-valued across twenty nodes) are not expressible as a value or pointer
/// scan, and <c>CalibrationResult</c>'s constructor is internal to LiveClr.
/// </para>
/// <para>
/// Three gates, all of them load-bearing (docs/analysis.md §12.5):
/// </para>
/// <list type="number">
/// <item>exactly one surviving candidate;</item>
/// <item><see cref="CompleteCoverage"/> — every byte of every scan window was readable. An
/// incomplete window can drop the true offset from one sample's set while a coincidence survives in
/// all of them, and the intersection then reports a <em>unique</em> and <em>wrong</em> answer;</item>
/// <item>for a semantic derivation, at least two samples carrying at least two <em>distinct</em>
/// expected values. One 200×50 control produced four candidates; a second control of the same size
/// would not have separated them.</item>
/// </list>
/// </remarks>
public sealed class OffsetCandidates
{
    private readonly int[] _candidates;
    private readonly HashSet<ulong> _sampleKeys;
    private readonly HashSet<string> _expectationKeys;

    /// <summary>
    /// Creates a candidate set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="sampleKeys"/> is one key per object scanned, normally its address, and is kept
    /// as a <em>set</em> rather than a count: intersecting the same sample twice must not satisfy the
    /// two-sample gate, which is exactly what a count would let it do.
    /// </para>
    /// <para>
    /// <paramref name="expectationKeys"/> is one key per expected value, also a set — two objects that
    /// happen to share a value cannot separate the real field from a co-varying neighbour, so they
    /// count once between them.
    /// </para>
    /// </remarks>
    public OffsetCandidates(
        string description,
        IEnumerable<int> candidates,
        CalibrationTechnique technique,
        IEnumerable<ulong> sampleKeys,
        IEnumerable<string> expectationKeys,
        bool completeCoverage)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(sampleKeys);
        ArgumentNullException.ThrowIfNull(expectationKeys);

        Description = description ?? string.Empty;
        _candidates = [.. candidates.Distinct().Order()];
        _sampleKeys = [.. sampleKeys];
        _expectationKeys = [.. expectationKeys];
        Technique = technique;
        CompleteCoverage = completeCoverage;
    }

    /// <summary>What was being looked for; only ever used to make a failure readable.</summary>
    public string Description { get; }

    /// <summary>Ascending, distinct byte offsets that satisfied every sample.</summary>
    public IReadOnlyList<int> Candidates => _candidates;

    /// <summary>Which gate applies before a lone candidate counts as an answer.</summary>
    public CalibrationTechnique Technique { get; }

    /// <summary>Distinct objects scanned.</summary>
    public int SampleCount => _sampleKeys.Count;

    /// <summary>Distinct expected values the samples carried.</summary>
    public int DistinctExpectationCount => _expectationKeys.Count;

    /// <summary>True when every byte of every scan window was readable.</summary>
    public bool CompleteCoverage { get; }

    /// <summary>Exactly one candidate survived.</summary>
    public bool IsUnique => _candidates.Length == 1;

    /// <summary>More than one candidate survived — add a sample with a different expected value.</summary>
    public bool IsAmbiguous => _candidates.Length > 1;

    /// <summary>Nothing survived.</summary>
    public bool IsEmpty => _candidates.Length == 0;

    /// <summary>True when <see cref="TryGetOffset"/> will succeed.</summary>
    public bool IsDetermined
        => IsUnique
        && CompleteCoverage
        && (Technique == CalibrationTechnique.Structural
            || (SampleCount >= 2 && DistinctExpectationCount >= 2));

    /// <summary>The single derived offset, when all three gates are satisfied.</summary>
    public bool TryGetOffset(out int offset)
    {
        offset = IsDetermined ? _candidates[0] : 0;
        return IsDetermined;
    }

    /// <summary>Why this set is not an answer, in one line. Empty when it is one.</summary>
    public string Obstacle()
    {
        if (IsDetermined)
        {
            return string.Empty;
        }

        if (IsEmpty)
        {
            return "no offset satisfied every sample";
        }

        if (IsAmbiguous)
        {
            return $"{_candidates.Length} candidates survived ({string.Join(", ", _candidates.Select(c => "0x" + c.ToString("x", System.Globalization.CultureInfo.InvariantCulture)))}); "
                 + "another sample with a different expected value is needed";
        }

        if (!CompleteCoverage)
        {
            return "part of a scan window was unreadable, so a candidate may have been dropped silently";
        }

        return SampleCount < 2
            ? "derived from a single sample; §12.5 showed one control yields four candidates"
            : "every sample carried the same expected value, which cannot separate a field from a co-varying neighbour";
    }

    /// <summary>Keeps only offsets present in both sets. Coverage and technique are conjunctive.</summary>
    public OffsetCandidates Intersect(OffsetCandidates other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new OffsetCandidates(
            Description == other.Description ? Description : $"{Description} ∩ {other.Description}",
            _candidates.Intersect(other._candidates),
            // A semantic input taints the whole derivation: the weaker claim wins.
            Technique == CalibrationTechnique.Semantic || other.Technique == CalibrationTechnique.Semantic
                ? CalibrationTechnique.Semantic
                : CalibrationTechnique.Structural,
            _sampleKeys.Union(other._sampleKeys),
            _expectationKeys.Union(other._expectationKeys),
            CompleteCoverage && other.CompleteCoverage);
    }

    /// <summary>Intersects a series. An empty series yields an empty, undetermined set.</summary>
    public static OffsetCandidates Intersect(string description, IEnumerable<OffsetCandidates> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        OffsetCandidates? accumulated = null;
        foreach (OffsetCandidates result in results)
        {
            accumulated = accumulated is null ? result : accumulated.Intersect(result);
        }

        return accumulated is null
            ? new OffsetCandidates(description, [], CalibrationTechnique.Semantic, [], [], false)
            : new OffsetCandidates(
                description,
                accumulated.Candidates,
                accumulated.Technique,
                accumulated._sampleKeys,
                accumulated._expectationKeys,
                accumulated.CompleteCoverage);
    }

    /// <summary>
    /// Adopts a LiveClr probe result. The keys are supplied by the caller because
    /// <see cref="CalibrationResult"/> publishes counts rather than identities, and counts cannot be
    /// unioned without double-counting a sample that appears in two intersected results.
    /// </summary>
    public static OffsetCandidates From(
        CalibrationResult result,
        IEnumerable<ulong> sampleKeys,
        IEnumerable<string> expectationKeys)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new OffsetCandidates(
            result.Description,
            result.Candidates,
            result.Technique,
            sampleKeys,
            expectationKeys,
            result.CompleteCoverage);
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"{Description}: [{string.Join(", ", _candidates.Select(c => "0x" + c.ToString("x", System.Globalization.CultureInfo.InvariantCulture)))}] "
         + $"from {SampleCount} sample(s), {Technique}{(CompleteCoverage ? string.Empty : ", INCOMPLETE COVERAGE")}";
}
