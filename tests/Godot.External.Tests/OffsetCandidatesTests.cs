using Godot.External.Calibrator.Calibration;
using LiveClr.Calibration;

namespace Godot.External.Tests;

/// <summary>
/// The three gates that stand between a candidate set and an answer (docs/analysis.md §12.5).
/// </summary>
public sealed class OffsetCandidatesTests
{
    private static OffsetCandidates Semantic(int[] candidates, int samples = 2, int distinct = 2, bool coverage = true)
        => new(
            "test",
            candidates,
            CalibrationTechnique.Semantic,
            Enumerable.Range(1, samples).Select(i => (ulong)(i * 0x1000)),
            Enumerable.Range(1, distinct).Select(i => $"value{i}"),
            coverage);

    [Fact]
    public void OneSampleIsNeverAnAnswerForASemanticDerivation()
    {
        OffsetCandidates single = Semantic([0x4c0], samples: 1, distinct: 1);

        Assert.True(single.IsUnique);
        Assert.False(single.IsDetermined);
        Assert.False(single.TryGetOffset(out _));
        Assert.Contains("single sample", single.Obstacle(), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoSamplesCarryingTheSameValueCannotSeparateAField()
    {
        // §12.5's collapse worked because the second control had a DIFFERENT size. Two 200x50
        // controls would have agreed on all four wrong candidates as readily as on the right one.
        OffsetCandidates same = Semantic([0x4c0], samples: 2, distinct: 1);

        Assert.False(same.IsDetermined);
        Assert.Contains("same expected value", same.Obstacle(), StringComparison.Ordinal);
    }

    [Fact]
    public void IntersectionCollapsesTheFourCandidateCase()
    {
        OffsetCandidates small = new("200x50", [0x4c0, 0x4c8, 0x4d4, 0x4f4], CalibrationTechnique.Semantic, [0x1000], ["200x50"], true);
        OffsetCandidates wide = new("1920x1080", [0x4c0, 0x510], CalibrationTechnique.Semantic, [0x2000], ["1920x1080"], true);

        OffsetCandidates intersected = small.Intersect(wide);

        Assert.True(intersected.TryGetOffset(out int offset));
        Assert.Equal(0x4c0, offset);
        Assert.Equal(2, intersected.SampleCount);
    }

    [Fact]
    public void IncompleteCoverageBlocksAUniqueCandidate()
    {
        // The failure this rule exists for: an unreadable stretch can drop the TRUE offset from one
        // sample's set while a coincidence survives in all of them, and the intersection then reports
        // a unique and wrong answer.
        OffsetCandidates partial = Semantic([0x4c0], coverage: false);

        Assert.True(partial.IsUnique);
        Assert.False(partial.IsDetermined);
        Assert.Contains("unreadable", partial.Obstacle(), StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageIsConjunctiveAcrossAnIntersection()
    {
        OffsetCandidates complete = Semantic([0x4c0, 0x4c8]);
        OffsetCandidates holed = Semantic([0x4c0], coverage: false);

        Assert.False(complete.Intersect(holed).CompleteCoverage);
    }

    [Fact]
    public void ASemanticInputTaintsAStructuralIntersection()
    {
        OffsetCandidates structural = new("ptr", [0x128], CalibrationTechnique.Structural, [0x1000], ["0xdead"], true);
        OffsetCandidates semantic = Semantic([0x128], samples: 1, distinct: 1);

        OffsetCandidates mixed = structural.Intersect(semantic);

        Assert.Equal(CalibrationTechnique.Semantic, mixed.Technique);
        Assert.False(mixed.IsDetermined);
    }

    [Fact]
    public void OneStructuralSampleIsEnough()
    {
        // A live address exists once in the process, so pointer identity does not need the
        // two-distinct-values gate that a size or a count does.
        OffsetCandidates structural = new("parent", [0x128], CalibrationTechnique.Structural, [0x1000], ["0xdead"], true);

        Assert.True(structural.TryGetOffset(out int offset));
        Assert.Equal(0x128, offset);
    }

    [Fact]
    public void AmbiguityAsksForADifferentSampleRatherThanPicking()
    {
        OffsetCandidates ambiguous = Semantic([0x4c0, 0x4c8]);

        Assert.False(ambiguous.TryGetOffset(out _));
        Assert.Contains("different expected value", ambiguous.Obstacle(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptySeriesIsUndeterminedRatherThanVacuouslyTrue()
    {
        OffsetCandidates empty = OffsetCandidates.Intersect("nothing", []);

        Assert.True(empty.IsEmpty);
        Assert.False(empty.IsDetermined);
        Assert.False(empty.CompleteCoverage);
    }

    [Fact]
    public void CandidatesAreDistinctAndAscending()
    {
        OffsetCandidates messy = Semantic([0x4f4, 0x4c0, 0x4c0, 0x4d4]);

        Assert.Equal(new[] { 0x4c0, 0x4d4, 0x4f4 }, messy.Candidates);
    }
}
