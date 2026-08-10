using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;

namespace VoidNote.Mandachord.Generation;

public enum MandachordSourceKind { MidiTrack, VoidNoteTrack, AudioTranscriptionTrack, StemDerivedMidiTrack, ShawzinTrack, AnalyzedAudioRegion }
public sealed record MandachordRhythmEvent(MusicalTime Start, MandachordPercussionCategory Category, decimal Strength, Guid? SourceEventId = null);
public sealed record MandachordSourceTrack(Guid Id, string Name, MandachordSourceKind Kind, IReadOnlyList<MusicalEvent> Events, MandachordLayer? PreferredLayer = null,
    Guid? SourceStemId = null, Guid? AudioRegionId = null, IReadOnlyList<MandachordRhythmEvent>? RhythmEvents = null)
{
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("Mandachord sources need identity and name.");
        if (Kind == MandachordSourceKind.AnalyzedAudioRegion && AudioRegionId is null) throw new InvalidOperationException("Raw AudioRegion input is forbidden; analyzed region identity is required.");
        if (Kind == MandachordSourceKind.StemDerivedMidiTrack && SourceStemId is null) throw new InvalidOperationException("Stem-derived MIDI must preserve its stem provenance.");
    }
}

public sealed record MandachordGenerationSettings
{
    public MusicalTime LoopStart { get; init; } = MusicalTime.Zero;
    public int CandidateCount { get; init; } = 3;
    public int Transposition { get; init; }
    public decimal MaximumLayerDensity { get; init; } = 0.5m;
    public Guid? SoundSetId { get; init; }
    public string SectionName { get; init; } = "Loop";
}

public sealed record MandachordGenerationCandidate(MandachordArrangement Arrangement, MandachordGenerationReport Report, int Rank);
public sealed record MandachordGenerationResult(IReadOnlyList<MandachordGenerationCandidate> Candidates);

public interface IMandachordGenerator
{
    MandachordGenerationResult Generate(ProjectTimeline timeline, IReadOnlyList<MandachordSourceTrack> sources,
        MandachordGenerationPreset preset, MandachordGenerationSettings settings);
}
