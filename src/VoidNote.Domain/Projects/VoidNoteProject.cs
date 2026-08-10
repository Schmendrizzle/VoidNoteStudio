using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Creator;

namespace VoidNote.Domain.Projects;

/// <summary>The normalized, versioned root model exchanged by every VoidNote module.</summary>
public sealed class VoidNoteProject
{
    /// <summary>The project format version produced by this milestone.</summary>
    public const int CurrentFormatVersion = 3;

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

    /// <summary>Gets or initializes audio tracks placed on the master timeline.</summary>
    public List<AudioTrack> AudioTracks { get; init; } = [];

    /// <summary>Gets or initializes non-destructive timeline selections.</summary>
    public List<AudioRegion> AudioRegions { get; init; } = [];

    /// <summary>Gets or initializes separated-stem references.</summary>
    public List<StemSet> StemSets { get; init; } = [];

    /// <summary>Preserves incomplete version-1 stem placeholders without silently inventing provenance.</summary>
    public List<LegacyStemReference> LegacyStemReferences { get; init; } = [];

    /// <summary>Gets persisted transcription reports and their cleanup audit trail.</summary>
    public List<AudioTranscriptionReport> AudioTranscriptionReports { get; init; } = [];

    /// <summary>Remembers the loaded schema for safe migration backups; it is not serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int LoadedFormatVersion { get; set; } = CurrentFormatVersion;

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
            .Concat(AudioTracks)
            .Concat(AudioTracks.SelectMany(track => track.Clips))
            .Concat(AudioRegions)
            .Concat(StemSets)
            .Concat(StemSets.SelectMany(set => set.StemTracks))
            .Concat(LegacyStemReferences)
            .Concat(AudioTranscriptionReports)
            .Concat(MidiTracks)
            .Concat(ShawzinTracks)
            .Concat(MandachordTracks)
            .Concat(CreatorSessions)
            .Concat(CreatorSessions.SelectMany(session => session.Sections))
            .Concat(CreatorSessions.SelectMany(session => session.Takes))
            .Select(item => item.Id)
            .ToArray();

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
        {
            throw new InvalidOperationException("Project item IDs must be non-empty and unique.");
        }

        foreach (var source in AudioSources)
        {
            if (source.File is null || source.Format.SampleRate <= 0 || source.Format.ChannelCount <= 0)
                throw new InvalidOperationException("Audio sources require a file reference and valid format information.");
        }

        var sourceIds = AudioSources.Select(source => source.Id).ToHashSet();
        if (AudioTracks.SelectMany(track => track.Clips).Any(clip => !sourceIds.Contains(clip.SourceId)))
            throw new InvalidOperationException("Every audio clip must reference a project audio source.");

        foreach (var region in AudioRegions) region.Validate();

        foreach (var set in StemSets)
        {
            if (!sourceIds.Contains(set.Source.AudioSourceId))
                throw new InvalidOperationException("Every stem set must reference a project audio source.");
            if (set.StemTracks.Any(stem => stem.StemSetId != set.Id || !sourceIds.Contains(stem.AudioSourceId)))
                throw new InvalidOperationException("Every stem must belong to its set and reference an imported derived audio source.");
        }

        var midiIds = MidiTracks.Select(track => track.Id).ToHashSet();
        if (AudioTranscriptionReports.Any(report => !midiIds.Contains(report.MidiTrackId)))
            throw new InvalidOperationException("Every transcription report must reference a MIDI track in the project.");

        if (CreatorSessions.Any(session => session.ProjectId != Id))
            throw new InvalidOperationException("Every creator session must belong to this project.");
        foreach (var session in CreatorSessions) session.Validate();
    }
}
