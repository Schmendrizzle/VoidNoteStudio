using VoidNote.Domain.Music;

namespace VoidNote.Domain.Projects;

/// <summary>Provides the common identity and name of a project item.</summary>
public abstract class ProjectItem
{
    /// <summary>Gets or initializes the stable item identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets or initializes the item name.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Provides a file-backed project asset.</summary>
public abstract class ProjectAsset : ProjectItem
{
    /// <summary>Gets or initializes the referenced file.</summary>
    public ProjectFileReference? File { get; init; }
}

/// <summary>Represents an audio source entry without implementing audio processing.</summary>
public sealed class AudioSource : ProjectAsset;

/// <summary>Represents a separated stem entry without implementing separation.</summary>
public sealed class Stem : ProjectAsset
{
    /// <summary>Gets or initializes the source audio item, when known.</summary>
    public Guid? SourceAudioId { get; init; }
}

/// <summary>Provides normalized events shared by future track implementations.</summary>
public abstract class ProjectTrack : ProjectItem
{
    /// <summary>Gets or initializes the normalized musical events.</summary>
    public List<MusicalEvent> Events { get; init; } = [];
}

/// <summary>Represents the foundation of a MIDI track; import and export are not implemented.</summary>
public sealed class MidiTrack : ProjectTrack;

/// <summary>Represents the foundation of a Shawzin track; codec and playback are not implemented.</summary>
public sealed class ShawzinTrack : ProjectTrack;

/// <summary>Represents the foundation of a Mandachord track; arrangement is not implemented.</summary>
public sealed class MandachordTrack : ProjectTrack;

/// <summary>Represents a future creator session boundary without creator workflow logic.</summary>
public sealed class CreatorSession : ProjectItem;
