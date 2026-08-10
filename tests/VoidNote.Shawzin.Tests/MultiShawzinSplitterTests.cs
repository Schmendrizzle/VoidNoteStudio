using System.Text.Json;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Tests;

public sealed class MultiShawzinSplitterTests
{
    private readonly IMultiShawzinSplitter _splitter = new MultiShawzinSplitter(new VoiceSalienceAnalyzer());

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void PolyphonicPassage_UsesRequestedExtensibleTrackCountWithoutSilentLoss(int count)
    {
        var notes = Enumerable.Range(0, count * 3).Select(index => Note(index / count * 960, 48 + index % count * 5)).ToArray();
        var result = Split(Track(notes), count, MultiShawzinSplitStrategy.FullEnsemble);

        Assert.Equal(count, result.Voices.Count);
        Assert.Equal(notes.Length, result.Report.Metrics.SourceNoteCount);
        Assert.Equal(notes.Length, result.Report.Metrics.AssignedNoteCount);
        Assert.Equal(0, result.Report.Metrics.DroppedNoteCount);
        Assert.Equal(0m, result.Report.Metrics.NoteLossPercent);
        Assert.Equal(notes.Select(value => value.Id).Order(), result.Voices.SelectMany(value => value.SourceTrack.Events).Select(value => value.Id).Order());
    }

    [Theory]
    [InlineData(MultiShawzinSplitStrategy.MelodyHarmony)]
    [InlineData(MultiShawzinSplitStrategy.MelodyBass)]
    [InlineData(MultiShawzinSplitStrategy.RegisterSplit)]
    [InlineData(MultiShawzinSplitStrategy.FullEnsemble)]
    [InlineData(MultiShawzinSplitStrategy.MinimalNoteLoss)]
    [InlineData(MultiShawzinSplitStrategy.MaximumRecognition)]
    [InlineData(MultiShawzinSplitStrategy.CreatorMultitrack)]
    public void EveryStrategy_IsDeterministicAndAudited(MultiShawzinSplitStrategy strategy)
    {
        var track = Track(Note(0, 40, 960), Note(0, 60), Note(480, 64), Note(960, 42, 960), Note(960, 67));
        var first = Split(track, 3, strategy);
        var second = Split(track, 3, strategy);

        Assert.Equal(first.Voices.Select(value => value.Id), second.Voices.Select(value => value.Id));
        Assert.Equal(first.Report.Assignments, second.Report.Assignments);
        Assert.All(first.Report.Assignments, value => Assert.False(string.IsNullOrWhiteSpace(value.Reason)));
        Assert.All(first.Report.Assignments, value => Assert.InRange(value.Confidence, 0m, 1m));
    }

    [Fact]
    public void MelodyBass_KeepsLeadAndBassContinuityInsteadOfOnlySelectingLocalExtremes()
    {
        var result = Split(Track(
            Note(0, 40, 960), Note(0, 67),
            Note(960, 41, 960), Note(960, 69),
            Note(1920, 43, 960), Note(1920, 71)), 2, MultiShawzinSplitStrategy.MelodyBass);

        Assert.Equal("Lead", result.Voices[0].DisplayName);
        Assert.Equal("Bass", result.Voices[1].DisplayName);
        Assert.Equal(3, result.Voices[0].SourceTrack.Events.Count);
        Assert.Equal(3, result.Voices[1].SourceTrack.Events.Count);
        Assert.True(result.Report.Metrics.VoiceContinuityScore > 70m);
    }

    [Fact]
    public void TrackBalance_ReportsPlausibleDistribution()
    {
        var result = Split(Track(Enumerable.Range(0, 12).Select(index => Note(index / 3 * 960, 48 + index % 3 * 7)).ToArray()), 3, MultiShawzinSplitStrategy.FullEnsemble);

        Assert.Equal([33.3m, 33.3m, 33.3m], result.Report.Metrics.TrackDistributionPercent);
        Assert.Equal(100m, result.Report.Metrics.BalanceScore);
    }

    [Fact]
    public void GoldenEnsembleFixtures_HaveExpectedDistributionAndNoUnreportedLoss()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ensembles", "milestone-f-cases.json");
        var fixtures = JsonSerializer.Deserialize<List<Fixture>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(10, fixtures.Count);
        foreach (var fixture in fixtures)
        {
            var notes = fixture.Notes.Select((value, index) => Note(value[0], value[1], value[2], index + 1)).ToArray();
            var result = Split(Track(notes), fixture.Shawzins, Enum.Parse<MultiShawzinSplitStrategy>(fixture.Strategy));
            Assert.Equal(fixture.ExpectedTotal ?? notes.Length, result.Report.Metrics.AssignedNoteCount);
            if (fixture.ExpectedCounts is not null) Assert.Equal(fixture.ExpectedCounts, result.Voices.Select(value => value.SourceTrack.Events.Count));
            Assert.Equal(result.Report.Metrics.SourceNoteCount, result.Report.Metrics.AssignedNoteCount + result.Report.Metrics.DroppedNoteCount);
            if (fixture.ExpectedArrangementLoss == true)
            {
                var mapper = new ShawzinPitchMapper();
                var arranger = new ShawzinEnsembleArranger(new ShawzinScaleAnalyzer(), new ShawzinTranspositionAnalyzer(mapper),
                    new ShawzinCompatibilityAnalyzer(mapper), new ShawzinArranger(mapper));
                var ensemble = arranger.Arrange(result, ProjectTimeline.CreateDefault());
                Assert.True(ensemble.OptimizationReport!.DroppedNoteCount > 0);
                Assert.NotEmpty(ensemble.SplitReport.LaterArrangementChanges);
            }
        }
    }

    private MultiShawzinSplitResult Split(MidiTrack track, int count, MultiShawzinSplitStrategy strategy) =>
        _splitter.Split([track], new MultiShawzinSplitOptions { ShawzinCount = count, Strategy = strategy });
    private static MidiTrack Track(params MusicalEvent[] notes) => new() { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Piano", Events = [.. notes] };
    private static MusicalEvent Note(long tick, int pitch, long duration = 480, int discriminator = 0) => new(
        Guid.Parse($"{discriminator:x8}-0000-{pitch:x4}-{tick & 0xffff:x4}-{tick:x12}"), new MusicalTime(tick), new MusicalTime(duration), pitch, 100,
        MusicalEventSource.ImportedMidi, 1m);
    private sealed record Fixture(string Name, int Shawzins, string Strategy, int[][] Notes, int[]? ExpectedCounts, int? ExpectedTotal, bool? ExpectedArrangementLoss);
}
