namespace VoidNote.Domain.Shawzin;

/// <summary>Identifies one of the three Shawzin strings.</summary>
public enum ShawzinString
{
    /// <summary>The first string.</summary>
    First = 1,
    /// <summary>The second string.</summary>
    Second = 2,
    /// <summary>The third string.</summary>
    Third = 3,
}

/// <summary>Identifies the fret buttons held for a Shawzin event.</summary>
[Flags]
public enum ShawzinFret
{
    /// <summary>No fret button.</summary>
    None = 0,
    /// <summary>The sky fret.</summary>
    Sky = 1,
    /// <summary>The earth fret.</summary>
    Earth = 2,
    /// <summary>The water fret.</summary>
    Water = 4,
}

/// <summary>Represents one sounded string under a single fret-button combination.</summary>
public sealed record ShawzinNote
{
    /// <summary>Creates a validated physical Shawzin note.</summary>
    public ShawzinNote(ShawzinString @string, ShawzinFret frets)
    {
        if (!Enum.IsDefined(@string))
        {
            throw new ArgumentOutOfRangeException(nameof(@string));
        }

        if ((frets & ~(ShawzinFret.Sky | ShawzinFret.Earth | ShawzinFret.Water)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frets));
        }

        String = @string;
        Frets = frets;
    }

    /// <summary>Gets the sounded string.</summary>
    public ShawzinString String { get; }

    /// <summary>Gets the held fret buttons.</summary>
    public ShawzinFret Frets { get; }
}
