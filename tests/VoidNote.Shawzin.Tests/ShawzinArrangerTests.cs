using System.Text.Json;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinArrangerTests
{
    private readonly ShawzinDefinition _instrument = BuiltInShawzinDefinitions.Default;
    private readonly IShawzinArranger _arranger = new ShawzinArranger(new ShawzinPitchMapper());

    [Fact]
    public void Strict_RejectsUnsupportedPitchWithExplicitConflict()
    {
        var result = Arrange(Track(Note(0, 49)), ArrangementStrategy.Strict, ShawzinScale.Major);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Report.Changes, change => change.ChangeType == ArrangementChangeType.ConflictUnresolved);
    }

    [Fact]
    public void ClosestPitch_SubstitutesAndReportsUnavailablePitch()
    {
        var result = Arrange(Track(Note(0, 49)), ArrangementStrategy.ClosestPitch, ShawzinScale.Major);

        Assert.True(result.IsSuccess);
        Assert.Equal(48, Assert.Single(result.Track!.Events).Pitch);
        Assert.Contains(result.Report.Changes, change => change.ChangeType == ArrangementChangeType.PitchSubstitution && change.Strategy == ArrangementStrategy.ClosestPitch);
    }

    [Fact]
    public void PartiallyPlayableChord_RemainsUnresolvedInStrictMode()
    {
        var result = Arrange(Track(Note(0, 48), Note(0, 49), Note(0, 52)), ArrangementStrategy.Strict, ShawzinScale.Major);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Report.Changes, change => change.SourcePitch == 49 && change.ChangeType == ArrangementChangeType.ConflictUnresolved);
    }

    [Fact]
    public void OctaveShift_ReportsSourceTargetAndStrategy()
    {
        var source = Note(0, 36);
        var result = Arrange(Track(source), ArrangementStrategy.OctaveShift);

        Assert.True(result.IsSuccess);
        var change = Assert.Single(result.Report.Changes, value => value.ChangeType == ArrangementChangeType.OctaveShift);
        Assert.Equal(source.Id, change.SourceEventId);
        Assert.Equal(36, change.SourcePitch);
        Assert.Equal(48, change.TargetPitch);
        Assert.Equal(ArrangementStrategy.OctaveShift, change.Strategy);
    }

    [Fact]
    public void ConfiguredTransposition_IsNeverAppliedSilently()
    {
        var result = _arranger.Arrange(Track(Note(0, 58)), Timeline, _instrument, new ArrangementOptions
        {
            Scale = ShawzinScale.Chromatic,
            Strategies = ArrangementStrategy.Strict,
            AllowTransposition = true,
            TranspositionSemitones = 2,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("dax", result.Track!.InstrumentId);
        Assert.Equal(60, Assert.Single(result.Track.Events).Pitch);
        Assert.Contains(result.Report.Changes, value => value.ChangeType == ArrangementChangeType.Transposed);
    }

    [Fact]
    public void ValidChord_IsPreservedAsOnePhysicalEvent()
    {
        var result = Arrange(Track(Note(0, 48), Note(0, 55), Note(0, 62)), ArrangementStrategy.Strict);

        Assert.True(result.IsSuccess);
        var output = Assert.Single(result.Track!.ShawzinEvents);
        Assert.Equal(3, output.Chord.Notes.Count);
        Assert.Equal(3, result.Report.OutputNoteCount);
    }

    [Theory]
    [InlineData(ArrangementStrategy.PreserveMelody, 62)]
    [InlineData(ArrangementStrategy.DropLowest, 62)]
    [InlineData(ArrangementStrategy.DropHighest, 48)]
    public void VoicePolicies_SelectExpectedEdgeVoice(ArrangementStrategy strategy, int expectedPitch)
    {
        var result = Arrange(Track(Note(0, 48), Note(0, 52), Note(0, 55), Note(0, 62)), strategy);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Track!.Events, value => value.Pitch == expectedPitch);
        Assert.Equal(3, result.Track.Events.Count);
        Assert.Single(result.Report.Changes, value => value.ChangeType == ArrangementChangeType.DroppedNote);
    }

    [Fact]
    public void Arpeggiate_DistributesInvalidChordWithinConfiguredBound()
    {
        var result = Arrange(Track(Note(0, 48), Note(0, 52), Note(0, 55)), ArrangementStrategy.Arpeggiate);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Track!.ShawzinEvents.Count);
        Assert.Equal([0m, 0.0625m, 0.125m], result.Track.ShawzinEvents.Select(value => value.Position.Seconds));
        Assert.Equal(2, result.Report.Changes.Count(value => value.ChangeType == ArrangementChangeType.Arpeggiated));
    }

    [Fact]
    public void Quantization_ReportsErrorAndResolvesCollisionOnlyWhenConfigured()
    {
        var strict = Arrange(Track(Note(0, 60), Note(20, 62)), ArrangementStrategy.Strict);
        var repaired = Arrange(Track(Note(0, 60), Note(20, 62)), ArrangementStrategy.Arpeggiate);

        Assert.False(strict.IsSuccess);
        Assert.True(repaired.IsSuccess);
        Assert.Equal(1, repaired.Report.Timing.CollidedEvents);
        Assert.Equal(0.0520833333333333333333333333m, repaired.Report.Timing.MaximumErrorSeconds);
    }

    [Fact]
    public void Simplify_ReducesDensePassageAndReportsEveryDrop()
    {
        var notes = Enumerable.Range(0, 15).Select(index => Note(index * 120, 60 + index % 10)).ToArray();
        var result = Arrange(Track(notes), ArrangementStrategy.Simplify | ArrangementStrategy.PreserveMelody | ArrangementStrategy.Arpeggiate);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Report.OutputNoteCount);
        Assert.Equal(3, result.Report.Changes.Count(value => value.ChangeType == ArrangementChangeType.DroppedNote));
    }

    [Fact]
    public void Arrangement_IsDeterministicIncludingIdsAndCode()
    {
        var track = Track(Note(0, 60), Note(480, 62), Note(960, 64));
        var first = Arrange(track, ArrangementStrategy.Strict);
        var second = Arrange(track, ArrangementStrategy.Strict);
        var encoder = new WarframeShawzinCodeEncoder();

        Assert.Equal(first.Track!.ShawzinEvents.Select(value => value.Id), second.Track!.ShawzinEvents.Select(value => value.Id));
        Assert.Equal(encoder.Encode(new ShawzinSong(first.Track)).Code, encoder.Encode(new ShawzinSong(second.Track)).Code);
    }

    [Fact]
    public void GoldenArrangementFixtures_ProduceExpectedNoteCounts()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Arrangements", "milestone-d-cases.json");
        var fixtures = JsonSerializer.Deserialize<List<ArrangementFixture>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(9, fixtures.Count);
        foreach (var fixture in fixtures)
        {
            var notes = fixture.Notes.Select(value => Note(value[0], value[1])).ToArray();
            var strategy = Enum.Parse<ArrangementStrategy>(fixture.Strategy);
            var result = Arrange(Track(notes), strategy);
            Assert.True(result.IsSuccess, fixture.Name);
            Assert.Equal(fixture.ExpectedOutputNotes, result.Report.OutputNoteCount);
        }
    }

    private ShawzinArrangementResult Arrange(MidiTrack track, ArrangementStrategy strategies, ShawzinScale scale = ShawzinScale.Chromatic) =>
        _arranger.Arrange(track, Timeline, _instrument, new ArrangementOptions { Scale = scale, Strategies = strategies });
    private static ProjectTimeline Timeline { get; } = ProjectTimeline.CreateDefault();
    private static MidiTrack Track(params MusicalEvent[] notes) => new() { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Fixture", Events = [.. notes] };
    private static MusicalEvent Note(long tick, int pitch) => new(StableNoteId(tick, pitch), new MusicalTime(tick), new MusicalTime(240), pitch, 100, MusicalEventSource.ImportedMidi, 1m);
    private static Guid StableNoteId(long tick, int pitch) => Guid.Parse($"00000000-0000-0000-{pitch:x4}-{tick:x12}");
    private sealed record ArrangementFixture(string Name, string Strategy, int[][] Notes, int ExpectedOutputNotes);
}
