using VoidNote.Domain.Music;

namespace VoidNote.Domain.Projects;

/// <summary>The normalized, versioned root model exchanged by every VoidNote module.</summary>
public sealed class VoidNoteProject
{
    /// <summary>The project format version produced by this milestone.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Gets or initializes the serialized format version.</summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>Gets or initializes the stable project identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets or initializes project metadata.</summary>
    public ProjectMetadata Metadata { get; init; } = new();

    /// <summary>Gets or initializes the shared master timeline.</summary>
    public ProjectTimeline Timeline { get; init; } = ProjectTimeline.CreateDefault();

    /// <summary>Gets or initializes source audio references.</summary>
    public List<AudioSource> AudioSources { get; init; } = [];

    /// <summary>Gets or initializes separated-stem references.</summary>
    public List<Stem> Stems { get; init; } = [];

    /// <summary>Gets or initializes normalized MIDI tracks.</summary>
    public List<MidiTrack> MidiTracks { get; init; } = [];

    /// <summary>Gets or initializes normalized Shawzin tracks.</summary>
    public List<ShawzinTrack> ShawzinTracks { get; init; } = [];

    /// <summary>Gets or initializes normalized Mandachord tracks.</summary>
    public List<MandachordTrack> MandachordTracks { get; init; } = [];

    /// <summary>Gets or initializes creator session shells.</summary>
    public List<CreatorSession> CreatorSessions { get; init; } = [];

    /// <summary>Verifies root invariants after construction or deserialization.</summary>
    public void Validate()
    {
        if (FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidOperationException($"Unsupported project format version: {FormatVersion}.");
        }

        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("A project ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(Metadata.Title))
        {
            throw new InvalidOperationException("A project title cannot be empty.");
        }

        var ids = AudioSources.Cast<ProjectItem>()
            .Concat(Stems)
            .Concat(MidiTracks)
            .Concat(ShawzinTracks)
            .Concat(MandachordTracks)
            .Concat(CreatorSessions)
            .Select(item => item.Id)
            .ToArray();

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
        {
            throw new InvalidOperationException("Project item IDs must be non-empty and unique.");
        }
    }
}
