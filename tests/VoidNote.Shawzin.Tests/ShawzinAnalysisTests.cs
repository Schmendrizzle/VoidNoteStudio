using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinAnalysisTests
{
    private readonly ShawzinDefinition _instrument = BuiltInShawzinDefinitions.Default;
    private readonly IShawzinPitchMapper _mapper = new ShawzinPitchMapper();

    [Fact]
    public void CompatibilityScore_IsDerivedFromPitchAndConflictMetrics()
    {
        var track = Track(Note(0, 60), Note(0, 61), Note(0, 62), Note(0, 63), Note(20, 100));
        var report = new ShawzinCompatibilityAnalyzer(_mapper).Analyze(track, Timeline, _instrument, ShawzinScale.Chromatic);

        Assert.Equal(5, report.TotalNotes);
        Assert.True(report.DirectlyPlayableNotes > 0);
        Assert.True(report.PolyphonyConflicts > 0);
        Assert.True(report.QuantizationCollisions > 0);
        Assert.InRange(report.OverallScore, 0, 99);
    }

    [Fact]
    public void ScaleRanking_UsesPitchClassesAndDirectCoverage()
    {
        var track = Track(Note(0, 48), Note(480, 50), Note(960, 52), Note(1440, 55), Note(1920, 57));
        var candidates = new ShawzinScaleAnalyzer().Analyze(track, _instrument);

        Assert.Equal(9, candidates.Count);
        Assert.Equal(ShawzinScale.PentatonicMajor, candidates[0].Scale);
        Assert.Equal(100m, candidates[0].PitchClassFitPercent);
    }

    [Fact]
    public void TranspositionRanking_FindsBetterCandidateWithoutApplyingIt()
    {
        var track = Track(Note(0, 49), Note(480, 51), Note(960, 53));
        var candidates = new ShawzinTranspositionAnalyzer(_mapper).Analyze(track, Timeline, _instrument, ShawzinScale.Major);

        Assert.Equal(25, candidates.Count);
        Assert.True(candidates[0].Score >= candidates.Single(value => value.Semitones == 0).Score);
        Assert.Contains(candidates, value => value.Semitones == 0);
    }

    private static ProjectTimeline Timeline { get; } = ProjectTimeline.CreateDefault();
    private static MidiTrack Track(params MusicalEvent[] notes) => new() { Events = [.. notes] };
    private static MusicalEvent Note(long tick, int pitch) => new(Guid.NewGuid(), new MusicalTime(tick), new MusicalTime(240), pitch, 100, MusicalEventSource.Manual, 1m);
}
