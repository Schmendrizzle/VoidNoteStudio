using VoidNote.Domain.Music;

namespace VoidNote.Midi.Playback;

/// <summary>Identifies a normalized event sent by MIDI playback.</summary>
public enum ScheduledMidiEventKind
{
    /// <summary>Starts a note.</summary>
    NoteOn,
    /// <summary>Ends a note.</summary>
    NoteOff,
}

/// <summary>Represents an output event at an absolute master-timeline position.</summary>
public sealed record ScheduledMidiEvent(
    Guid EventId,
    Guid TrackId,
    AbsoluteTime Time,
    ScheduledMidiEventKind Kind,
    int Pitch,
    int Velocity);
