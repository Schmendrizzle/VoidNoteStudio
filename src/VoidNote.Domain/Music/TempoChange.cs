namespace VoidNote.Domain.Music;

/// <summary>Defines a tempo beginning at a position on the master timeline.</summary>
public sealed record TempoChange
{
    /// <summary>Creates a tempo change.</summary>
    public TempoChange(MusicalTime position, decimal beatsPerMinute)
    {
        if (beatsPerMinute <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), "Tempo must be greater than zero.");
        }

        Position = position;
        BeatsPerMinute = beatsPerMinute;
    }

    /// <summary>Gets the position at which this tempo becomes active.</summary>
    public MusicalTime Position { get; }

    /// <summary>Gets the tempo in quarter-note beats per minute.</summary>
    public decimal BeatsPerMinute { get; }
}
