using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Midi;

/// <summary>DryWetMIDI-backed format-1 Standard MIDI File exporter.</summary>
public sealed class DryWetMidiFileExporter : IMidiFileExporter
{
    /// <inheritdoc />
    public async Task ExportAsync(
        Stream destination,
        ProjectTimeline timeline,
        IReadOnlyList<MidiTrack> tracks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(tracks);
        cancellationToken.ThrowIfCancellationRequested();
        if (timeline.TicksPerQuarterNote > short.MaxValue)
        {
            throw new MidiFileException($"PPQ must not exceed {short.MaxValue} for Standard MIDI File export.");
        }

        var chunks = new List<MidiChunk> { BuildConductorTrack(timeline) };
        chunks.AddRange(tracks.Select((track, index) => BuildNoteTrack(track, index, cancellationToken)));

        var file = new MidiFile(chunks)
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision((short)timeline.TicksPerQuarterNote),
        };

        await using var buffered = new MemoryStream();
        file.Write(buffered, MidiFileFormat.MultiTrack);
        buffered.Position = 0;
        await buffered.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static TrackChunk BuildConductorTrack(ProjectTimeline timeline)
    {
        var events = new List<AbsoluteMidiEvent>
        {
            new(0, 0, new SequenceTrackNameEvent("VoidNote Tempo Map")),
        };

        events.AddRange(timeline.TempoChanges.Select(change =>
        {
            var microseconds = checked((long)decimal.Round(
                60_000_000m / change.BeatsPerMinute,
                0,
                MidpointRounding.AwayFromZero));
            return new AbsoluteMidiEvent(
                change.Position.Ticks,
                0,
                new SetTempoEvent(microseconds));
        }));
        events.AddRange(timeline.TimeSignatureChanges.Select(change =>
            new AbsoluteMidiEvent(
                change.Position.Ticks,
                0,
                new TimeSignatureEvent(
                    checked((byte)change.Numerator),
                    checked((byte)change.Denominator)))));

        return CreateTrackChunk(events);
    }

    private static TrackChunk BuildNoteTrack(
        MidiTrack track,
        int trackIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        var name = string.IsNullOrWhiteSpace(track.Name) ? $"Track {trackIndex + 1}" : track.Name;
        var events = new List<AbsoluteMidiEvent>
        {
            new(0, 0, new SequenceTrackNameEvent(name)),
        };

        foreach (var note in track.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var noteNumber = (SevenBitNumber)note.Pitch;
            var velocity = (SevenBitNumber)note.Velocity;
            events.Add(new AbsoluteMidiEvent(
                note.StartTime.Ticks,
                1,
                new NoteOnEvent(noteNumber, velocity)));
            events.Add(new AbsoluteMidiEvent(
                checked(note.StartTime.Ticks + note.Duration.Ticks),
                2,
                new NoteOffEvent(noteNumber, (SevenBitNumber)0)));
        }

        return CreateTrackChunk(events);
    }

    private static TrackChunk CreateTrackChunk(IEnumerable<AbsoluteMidiEvent> absoluteEvents)
    {
        long previousTime = 0;
        var events = absoluteEvents
            .OrderBy(item => item.Time)
            .ThenBy(item => item.SortOrder)
            .Select(item =>
            {
                item.Event.DeltaTime = checked(item.Time - previousTime);
                previousTime = item.Time;
                return item.Event;
            })
            .ToArray();
        return new TrackChunk(events);
    }

    private sealed record AbsoluteMidiEvent(long Time, int SortOrder, MidiEvent Event);
}
