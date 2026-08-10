using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Midi.Tests.Fixtures;

namespace VoidNote.Midi.Tests;

public sealed class MidiRoundTripTests
{
    private readonly IMidiFileImporter _importer = new DryWetMidiFileImporter();
    private readonly IMidiFileExporter _exporter = new DryWetMidiFileExporter();

    [Fact]
    public async Task ComplexFixture_RoundTripPreservesMusicalDataExactlyInTicks()
    {
        await using var fixture = MidiFixtureFactory.ComplexRoundTrip();
        var first = await _importer.ImportAsync(fixture);
        await using var exported = new MemoryStream();

        await _exporter.ExportAsync(exported, first.Timeline, first.Tracks);
        exported.Position = 0;
        var second = await _importer.ImportAsync(exported);

        Assert.Equal(first.Timeline.TicksPerQuarterNote, second.Timeline.TicksPerQuarterNote);
        Assert.Equal(first.Tracks.Count, second.Tracks.Count);
        for (var trackIndex = 0; trackIndex < first.Tracks.Count; trackIndex++)
        {
            var expectedTrack = first.Tracks[trackIndex];
            var actualTrack = second.Tracks[trackIndex];
            Assert.Equal(expectedTrack.Name, actualTrack.Name);
            Assert.Equal(expectedTrack.Events.Count, actualTrack.Events.Count);
            Assert.Equal(
                expectedTrack.Events.Select(NoteData),
                actualTrack.Events.Select(NoteData));
        }

        Assert.Equal(
            first.Timeline.TempoChanges.Select(change => (change.Position.Ticks, change.BeatsPerMinute)),
            second.Timeline.TempoChanges.Select(change => (change.Position.Ticks, change.BeatsPerMinute)));
        Assert.Equal(
            first.Timeline.TimeSignatureChanges.Select(change => (change.Position.Ticks, change.Numerator, change.Denominator)),
            second.Timeline.TimeSignatureChanges.Select(change => (change.Position.Ticks, change.Numerator, change.Denominator)));
    }

    [Fact]
    public async Task ExportTempoRounding_IsNearestWholeMicrosecondAndExplicitlyBounded()
    {
        var timeline = new ProjectTimeline(960, [new TempoChange(MusicalTime.Zero, 123.456m)]);
        var track = new MidiTrack
        {
            Name = "Rounding",
            Events = [CreateNote(0, 960, 60, 100)],
        };
        await using var exported = new MemoryStream();

        await _exporter.ExportAsync(exported, timeline, [track]);
        exported.Position = 0;
        var imported = await _importer.ImportAsync(exported);

        var expectedMicroseconds = decimal.Round(60_000_000m / 123.456m, 0, MidpointRounding.AwayFromZero);
        var expectedBpm = 60_000_000m / expectedMicroseconds;
        Assert.Equal(expectedBpm, imported.Timeline.TempoChanges[0].BeatsPerMinute);
        Assert.InRange(Math.Abs(expectedBpm - 123.456m), 0m, 0.0002m);
    }

    [Fact]
    public async Task Export_CanBeCancelledBeforeWriting()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _exporter.ExportAsync(destination, ProjectTimeline.CreateDefault(), [], cancellation.Token));
        Assert.Equal(0, destination.Length);
    }

    private static (long Start, long Duration, int Pitch, int Velocity) NoteData(MusicalEvent note) =>
        (note.StartTime.Ticks, note.Duration.Ticks, note.Pitch, note.Velocity);

    private static MusicalEvent CreateNote(long start, long duration, int pitch, int velocity) =>
        new(Guid.NewGuid(), new MusicalTime(start), new MusicalTime(duration), pitch, velocity, MusicalEventSource.Manual, 1m);
}
