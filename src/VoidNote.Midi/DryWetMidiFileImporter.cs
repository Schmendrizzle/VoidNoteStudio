using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Midi;

/// <summary>DryWetMIDI-backed Standard MIDI File importer.</summary>
public sealed class DryWetMidiFileImporter : IMidiFileImporter
{
    /// <inheritdoc />
    public async Task<MidiImportResult> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        await using var buffered = new MemoryStream();
        await source.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
        buffered.Position = 0;

        try
        {
            var file = MidiFile.Read(buffered);
            cancellationToken.ThrowIfCancellationRequested();
            if (file.TimeDivision is not TicksPerQuarterNoteTimeDivision timeDivision)
            {
                throw new MidiFileException("SMPTE time divisions are not supported; a PPQ MIDI file is required.");
            }

            var trackChunks = file.GetTrackChunks().ToArray();
            var timeline = BuildTimeline(trackChunks, timeDivision.TicksPerQuarterNote);
            var tracks = trackChunks
                .Where(chunk => chunk.GetNotes().Count > 0)
                .Select((chunk, index) => ImportTrack(chunk, index, cancellationToken))
                .ToArray();
            return new MidiImportResult(timeline, tracks);
        }
        catch (MidiFileException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new MidiFileException("The MIDI file could not be imported.", exception);
        }
    }

    private static ProjectTimeline BuildTimeline(
        IReadOnlyList<TrackChunk> chunks,
        int ticksPerQuarterNote)
    {
        var timedEvents = chunks
            .SelectMany((chunk, trackIndex) => chunk.GetTimedEvents()
                .Select((timedEvent, eventIndex) => new IndexedTimedEvent(timedEvent, trackIndex, eventIndex)))
            .OrderBy(item => item.Event.Time)
            .ThenBy(item => item.TrackIndex)
            .ThenBy(item => item.EventIndex)
            .ToArray();

        var tempos = timedEvents
            .Where(item => item.Event.Event is SetTempoEvent)
            .GroupBy(item => item.Event.Time)
            .Select(group => group.Last())
            .Select(item => new TempoChange(
                new MusicalTime(item.Event.Time),
                60_000_000m / ((SetTempoEvent)item.Event.Event).MicrosecondsPerQuarterNote))
            .ToList();
        EnsureTempoAtZero(tempos);

        var signatures = timedEvents
            .Where(item => item.Event.Event is TimeSignatureEvent)
            .GroupBy(item => item.Event.Time)
            .Select(group => group.Last())
            .Select(item =>
            {
                var signature = (TimeSignatureEvent)item.Event.Event;
                return new TimeSignatureChange(
                    new MusicalTime(item.Event.Time),
                    signature.Numerator,
                    signature.Denominator);
            })
            .ToList();
        EnsureTimeSignatureAtZero(signatures);

        return new ProjectTimeline(ticksPerQuarterNote, tempos, signatures);
    }

    private static MidiTrack ImportTrack(
        TrackChunk chunk,
        int index,
        CancellationToken cancellationToken)
    {
        var name = chunk.GetTimedEvents()
            .Select(item => item.Event)
            .OfType<SequenceTrackNameEvent>()
            .Select(item => item.Text)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? $"Track {index + 1}";

        var events = new List<MusicalEvent>();
        foreach (var note in chunk.GetNotes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add(new MusicalEvent(
                Guid.NewGuid(),
                new MusicalTime(note.Time),
                new MusicalTime(note.Length),
                note.NoteNumber,
                note.Velocity,
                MusicalEventSource.ImportedMidi,
                1m));
        }

        return new MidiTrack
        {
            Name = name,
            Events = events
                .OrderBy(note => note.StartTime.Ticks)
                .ThenBy(note => note.Pitch)
                .ToList(),
        };
    }

    private static void EnsureTempoAtZero(List<TempoChange> changes)
    {
        if (changes.Count == 0 || changes[0].Position != MusicalTime.Zero)
        {
            changes.Insert(0, new TempoChange(MusicalTime.Zero, 120m));
        }
    }

    private static void EnsureTimeSignatureAtZero(List<TimeSignatureChange> changes)
    {
        if (changes.Count == 0 || changes[0].Position != MusicalTime.Zero)
        {
            changes.Insert(0, new TimeSignatureChange(MusicalTime.Zero, 4, 4));
        }
    }

    private sealed record IndexedTimedEvent(TimedEvent Event, int TrackIndex, int EventIndex);
}
