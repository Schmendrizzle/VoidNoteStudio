namespace VoidNote.Domain.Music;

/// <summary>Represents a non-negative position or duration in timeline ticks.</summary>
public readonly record struct MusicalTime
{
    /// <summary>Creates a musical time value.</summary>
    /// <param name="ticks">The number of timeline ticks.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ticks"/> is negative.</exception>
    public MusicalTime(long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        Ticks = ticks;
    }

    /// <summary>Gets the number of timeline ticks.</summary>
    public long Ticks { get; }

    /// <summary>Gets the zero position.</summary>
    public static MusicalTime Zero => new(0);

    /// <summary>Adds two musical time values with overflow checking.</summary>
    public static MusicalTime operator +(MusicalTime left, MusicalTime right) =>
        new(checked(left.Ticks + right.Ticks));

    /// <summary>Subtracts two musical time values.</summary>
    public static MusicalTime operator -(MusicalTime left, MusicalTime right) =>
        new(checked(left.Ticks - right.Ticks));
}
