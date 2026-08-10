using VoidNote.Domain.Projects;

namespace VoidNote.Domain.Shawzin;

/// <summary>Contains physical Shawzin events alongside the normalized project-track boundary.</summary>
public sealed class ShawzinTrack : ProjectTrack
{
    /// <summary>Gets or initializes the scale required to interpret this track.</summary>
    public ShawzinScale Scale { get; init; } = ShawzinScale.PentatonicMinor;

    /// <summary>Gets or initializes physical Shawzin strikes in timeline order.</summary>
    public List<ShawzinEvent> ShawzinEvents { get; init; } = [];
}
