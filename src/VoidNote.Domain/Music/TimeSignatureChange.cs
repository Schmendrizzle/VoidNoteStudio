namespace VoidNote.Domain.Music;

/// <summary>Defines a time signature beginning at a position on the master timeline.</summary>
public sealed record TimeSignatureChange
{
    /// <summary>Creates a validated time signature change.</summary>
    public TimeSignatureChange(MusicalTime position, int numerator, int denominator)
    {
        if (numerator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator));
        }

        if (denominator <= 0 || (denominator & (denominator - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "The denominator must be a positive power of two.");
        }

        Position = position;
        Numerator = numerator;
        Denominator = denominator;
    }

    /// <summary>Gets the position at which this signature becomes active.</summary>
    public MusicalTime Position { get; }

    /// <summary>Gets the number of beats in a bar.</summary>
    public int Numerator { get; }

    /// <summary>Gets the note value represented by one signature beat.</summary>
    public int Denominator { get; }
}
