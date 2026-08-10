using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Application.Midi;

/// <summary>UI-framework-independent projection of one note for a future piano roll.</summary>
public sealed record PianoRollNoteViewModel(
    Guid Id,
    long StartTick,
    long DurationTicks,
    int Pitch,
    int Velocity,
    decimal Confidence,
    MusicalEventSource Source,
    VoidNote.Domain.Audio.NoteConfidenceLevel? ConfidenceLevel,
    VoidNote.Domain.Audio.DetectionEditStatus? DetectionStatus,
    decimal StartBeat,
    MusicalPosition MusicalPosition,
    AbsoluteTime AbsoluteStart);

/// <summary>Minimal read-only piano-roll data source; editing UI is intentionally deferred.</summary>
public sealed class PianoRollViewModel
{
    /// <summary>Creates a piano-roll projection from a normalized MIDI track.</summary>
    public PianoRollViewModel(MidiTrack track, ProjectTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(timeline);
        TrackId = track.Id;
        TrackName = track.Name;
        Notes = track.Events
            .OrderBy(note => note.StartTime.Ticks)
            .ThenBy(note => note.Pitch)
            .Select(note => new PianoRollNoteViewModel(note.Id, note.StartTime.Ticks, note.Duration.Ticks, note.Pitch, note.Velocity,
                note.Confidence, note.Source, note.AudioProvenance?.ConfidenceLevel, note.AudioProvenance?.EditStatus,
                timeline.ToBeats(note.StartTime), timeline.ToMusicalPosition(note.StartTime), timeline.ToAbsoluteTime(note.StartTime)))
            .ToArray();
    }

    /// <summary>Gets the projected track identifier.</summary>
    public Guid TrackId { get; }
    /// <summary>Gets the projected track name.</summary>
    public string TrackName { get; }
    /// <summary>Gets notes ordered for deterministic rendering.</summary>
    public IReadOnlyList<PianoRollNoteViewModel> Notes { get; }
}
