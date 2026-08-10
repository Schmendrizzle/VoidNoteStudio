namespace VoidNote.Domain.Shawzin;

/// <summary>Represents one to three strings struck with a shared fret combination.</summary>
public sealed class ShawzinChord
{
    private readonly IReadOnlyList<ShawzinNote> _notes;

    /// <summary>Creates a validated, canonically ordered chord.</summary>
    public ShawzinChord(IReadOnlyList<ShawzinNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        if (notes.Count is < 1 or > 3)
        {
            throw new ArgumentException("A Shawzin chord must sound between one and three strings.", nameof(notes));
        }

        if (notes.Any(note => note is null))
        {
            throw new ArgumentException("A Shawzin chord cannot contain a null note.", nameof(notes));
        }

        var ordered = notes.OrderBy(note => note.String).ToArray();
        if (ordered.Select(note => note.String).Distinct().Count() != ordered.Length)
        {
            throw new ArgumentException("A Shawzin string can occur only once in a chord.", nameof(notes));
        }

        if (ordered.Select(note => note.Frets).Distinct().Count() != 1)
        {
            throw new ArgumentException("Every note in a Shawzin chord must use the same fret combination.", nameof(notes));
        }

        _notes = ordered;
    }

    /// <summary>Gets the sounded notes in canonical string order.</summary>
    public IReadOnlyList<ShawzinNote> Notes => _notes;

    /// <summary>Gets the shared fret combination.</summary>
    public ShawzinFret Frets => _notes[0].Frets;
}
