using VoidNote.Domain.Music;

namespace VoidNote.Domain.Shawzin;

/// <summary>Represents an instantaneous Shawzin strike on the absolute master timeline.</summary>
public sealed record ShawzinEvent
{
    /// <summary>Creates a validated Shawzin event.</summary>
    public ShawzinEvent(Guid id, AbsoluteTime position, ShawzinChord chord)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Shawzin event ID cannot be empty.", nameof(id));
        }

        Id = id;
        Position = position;
        Chord = chord ?? throw new ArgumentNullException(nameof(chord));
    }

    /// <summary>Gets the stable event identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the exact absolute position before code-format quantization.</summary>
    public AbsoluteTime Position { get; }

    /// <summary>Gets the physical note or chord struck at this position.</summary>
    public ShawzinChord Chord { get; }

    /// <summary>Projects the absolute position onto a VoidNote project timeline.</summary>
    public MusicalTime ToMusicalTime(ProjectTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        return timeline.ToMusicalTime(Position);
    }
}
