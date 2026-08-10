using VoidNote.Domain.Music;
using VoidNote.Midi.Tests.Fixtures;

namespace VoidNote.Midi.Tests;

public sealed class MidiImportTests
{
    private readonly IMidiFileImporter _importer = new DryWetMidiFileImporter();

    [Fact]
    public async Task SingleNote_ImportsPitchVelocityTimingDurationAndName()
    {
        await using var fixture = MidiFixtureFactory.SingleNote();

        var result = await _importer.ImportAsync(fixture);

        var track = Assert.Single(result.Tracks);
        var note = Assert.Single(track.Events);
        Assert.Equal("Lead", track.Name);
        Assert.Equal(60, note.Pitch);
        Assert.Equal(91, note.Velocity);
        Assert.Equal(120, note.StartTime.Ticks);
        Assert.Equal(360, note.Duration.Ticks);
        Assert.Equal(MusicalEventSource.ImportedMidi, note.Source);
        Assert.Equal(0.125m, result.Timeline.ToAbsoluteTime(note.StartTime).Seconds);
    }

    [Fact]
    public async Task SequentialNotes_PreserveOrderAndDurations()
    {
        await using var fixture = MidiFixtureFactory.SequentialNotes();
        var result = await _importer.ImportAsync(fixture);

        Assert.Equal([60, 62, 64], result.Tracks[0].Events.Select(note => note.Pitch));
        Assert.Equal([240L, 240L, 480L], result.Tracks[0].Events.Select(note => note.Duration.Ticks));
    }

    [Fact]
    public async Task Chord_PreservesSimultaneousStarts()
    {
        await using var fixture = MidiFixtureFactory.Chord();
        var result = await _importer.ImportAsync(fixture);

        Assert.Equal(3, result.Tracks[0].Events.Count);
        Assert.All(result.Tracks[0].Events, note => Assert.Equal(0, note.StartTime.Ticks));
    }

    [Fact]
    public async Task MultipleTracks_PreserveTrackAssignmentAndNames()
    {
        await using var fixture = MidiFixtureFactory.MultipleTracks();
        var result = await _importer.ImportAsync(fixture);

        Assert.Equal(2, result.Tracks.Count);
        Assert.Equal(["Lead", "Bass"], result.Tracks.Select(track => track.Name));
        Assert.Equal(72, Assert.Single(result.Tracks[0].Events).Pitch);
        Assert.Equal(36, Assert.Single(result.Tracks[1].Events).Pitch);
    }

    [Fact]
    public async Task VelocityFixture_PreservesEntireMidiRange()
    {
        await using var fixture = MidiFixtureFactory.Velocities();
        var result = await _importer.ImportAsync(fixture);

        Assert.Equal([1, 64, 127], result.Tracks[0].Events.Select(note => note.Velocity));
    }

    [Fact]
    public async Task TempoChanges_AffectAbsoluteTimeBySegment()
    {
        await using var fixture = MidiFixtureFactory.TempoChange();
        var result = await _importer.ImportAsync(fixture);

        Assert.Equal(2, result.Timeline.TempoChanges.Count);
        Assert.Equal(120m, result.Timeline.TempoChanges[0].BeatsPerMinute);
        Assert.Equal(60m, result.Timeline.TempoChanges[1].BeatsPerMinute);
        Assert.Equal(3m, result.Timeline.ToAbsoluteTime(new MusicalTime(1_920)).Seconds);
    }

    [Fact]
    public async Task TimeSignatures_ImportAndDriveMusicalPositions()
    {
        await using var fixture = MidiFixtureFactory.TimeSignatures();
        var result = await _importer.ImportAsync(fixture);

        Assert.Equal(2, result.Timeline.TimeSignatureChanges.Count);
        Assert.Equal((3, 4), (
            result.Timeline.TimeSignatureChanges[1].Numerator,
            result.Timeline.TimeSignatureChanges[1].Denominator));
        Assert.Equal(new MusicalPosition(2, 1, 0), result.Timeline.ToMusicalPosition(new MusicalTime(1_920)));
        Assert.Equal(new MusicalPosition(3, 1, 0), result.Timeline.ToMusicalPosition(new MusicalTime(3_360)));
    }

    [Fact]
    public async Task LongTiming_UsesExactTicksWithoutCumulativeDrift()
    {
        await using var fixture = MidiFixtureFactory.LongTiming();
        var result = await _importer.ImportAsync(fixture);
        var note = Assert.Single(result.Tracks[0].Events);

        var absolute = result.Timeline.ToAbsoluteTime(note.StartTime);
        var roundTrip = result.Timeline.ToMusicalTime(absolute);

        Assert.Equal(1_000_000, note.StartTime.Ticks);
        Assert.Equal(123_456, note.Duration.Ticks);
        Assert.Equal(note.StartTime, roundTrip);
    }
}
