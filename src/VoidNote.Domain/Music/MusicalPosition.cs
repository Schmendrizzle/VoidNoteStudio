namespace VoidNote.Domain.Music;

/// <summary>Represents a one-based bar and beat plus a zero-based tick offset within the beat.</summary>
public readonly record struct MusicalPosition
{
    /// <summary>Creates a validated musical position.</summary>
    public MusicalPosition(long bar, int beat, long tickInBeat)
    {
        if (bar < 1) throw new ArgumentOutOfRangeException(nameof(bar));
        if (beat < 1) throw new ArgumentOutOfRangeException(nameof(beat));
        ArgumentOutOfRangeException.ThrowIfNegative(tickInBeat);

        Bar = bar;
        Beat = beat;
        TickInBeat = tickInBeat;
    }

    /// <summary>Gets the one-based bar number.</summary>
    public long Bar { get; }

    /// <summary>Gets the one-based beat number in the bar.</summary>
    public int Beat { get; }

    /// <summary>Gets the zero-based master-tick offset within the beat.</summary>
    public long TickInBeat { get; }
}
