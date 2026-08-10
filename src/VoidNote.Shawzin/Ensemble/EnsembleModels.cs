using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>Identifies the deterministic musical policy used to split source notes.</summary>
public enum MultiShawzinSplitStrategy
{
    MelodyHarmony,
    MelodyBass,
    RegisterSplit,
    FullEnsemble,
    MinimalNoteLoss,
    MaximumRecognition,
    CreatorMultitrack,
}

/// <summary>Optional per-voice preferences used during splitting and arrangement.</summary>
public sealed record ShawzinVoicePreference
{
    public int TrackIndex { get; init; }
    public ShawzinDefinition? Instrument { get; init; }
    public ShawzinScale? Scale { get; init; }
    public int? TranspositionSemitones { get; init; }
    public ArrangementStrategy? ArrangementStrategies { get; init; }
}

/// <summary>Configures a deterministic multi-Shawzin split.</summary>
public sealed record MultiShawzinSplitOptions
{
    public int ShawzinCount { get; init; } = 2;
    public MultiShawzinSplitStrategy Strategy { get; init; } = MultiShawzinSplitStrategy.FullEnsemble;
    public IReadOnlyList<ShawzinVoicePreference> Preferences { get; init; } = [];
}

/// <summary>Explains one source-note assignment.</summary>
public sealed record SplitAssignment(
    Guid SourceTrackId,
    Guid SourceEventId,
    int SourcePitch,
    MusicalTime SourceTime,
    Guid? TargetTrackId,
    string TargetTrackName,
    MultiShawzinSplitStrategy Strategy,
    decimal Score,
    decimal Confidence,
    string Reason,
    bool IsDuplicate = false,
    bool IsDropped = false);

/// <summary>Contains auditable aggregate metrics for one split.</summary>
public sealed record SplitMetrics(
    int SourceNoteCount,
    int AssignedNoteCount,
    int DroppedNoteCount,
    int DuplicateNoteCount,
    decimal NoteLossPercent,
    decimal VoiceContinuityScore,
    decimal BalanceScore,
    IReadOnlyList<decimal> TrackDistributionPercent);

/// <summary>Records every decision made by the voice separator.</summary>
public sealed record MultiShawzinSplitReport(
    MultiShawzinSplitStrategy Strategy,
    IReadOnlyList<SplitAssignment> Assignments,
    SplitMetrics Metrics,
    IReadOnlyList<ArrangementChange> LaterArrangementChanges)
{
    public IReadOnlyList<SplitAssignment> DroppedNotes => Assignments.Where(value => value.IsDropped).ToArray();
    public IReadOnlyList<SplitAssignment> DuplicateNotes => Assignments.Where(value => value.IsDuplicate).ToArray();
    public IReadOnlyList<ArrangementChange> ShiftedNotes => LaterArrangementChanges.Where(value =>
        value.ChangeType is ArrangementChangeType.Transposed or ArrangementChangeType.OctaveShift or
            ArrangementChangeType.PitchSubstitution or ArrangementChangeType.Arpeggiated or ArrangementChangeType.Quantized).ToArray();
}

/// <summary>One normalized voice produced before Shawzin-specific arrangement.</summary>
public sealed record SplitVoice(
    Guid Id,
    string DisplayName,
    MidiTrack SourceTrack,
    ShawzinVoicePreference? Preference);

/// <summary>Result of a pure normalized-model voice split.</summary>
public sealed record MultiShawzinSplitResult(
    IReadOnlyList<SplitVoice> Voices,
    MultiShawzinSplitReport Report);

/// <summary>One independently configurable and exportable member of an ensemble.</summary>
public sealed class ShawzinEnsembleTrack
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; set; }
    public required ShawzinDefinition Instrument { get; set; }
    public required ShawzinScale Scale { get; set; }
    public int TranspositionSemitones { get; set; }
    public ArrangementStrategy ArrangementStrategies { get; set; } = ArrangementStrategy.ClosestPitch | ArrangementStrategy.OctaveShift |
        ArrangementStrategy.PreserveMelody | ArrangementStrategy.Arpeggiate;
    public required MidiTrack SourceTrack { get; set; }
    public ShawzinTrack? ShawzinTrack { get; set; }
    public ArrangementReport? ArrangementReport { get; set; }
    public ShawzinCompatibilityReport? Compatibility { get; set; }
    public IReadOnlyList<ShawzinScaleCandidate> ScaleCandidates { get; set; } = [];
    public IReadOnlyList<ShawzinTranspositionCandidate> TranspositionCandidates { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
}

/// <summary>Several Shawzin voices sharing one immutable master-timeline reference.</summary>
public sealed class ShawzinEnsemble
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required ProjectTimeline MasterTimeline { get; init; }
    public List<ShawzinEnsembleTrack> Tracks { get; init; } = [];
    public required MultiShawzinSplitReport SplitReport { get; set; }
    public EnsembleOptimizationReport? OptimizationReport { get; set; }
}

/// <summary>Reports ensemble-wide compatibility, loss, duplication, pitch movement and stability.</summary>
public sealed record EnsembleOptimizationReport(
    int SourceNoteCount,
    int ArrangedNoteCount,
    int DroppedNoteCount,
    int DuplicateNoteCount,
    decimal NoteLossPercent,
    decimal AverageCompatibility,
    int LowestTrackCompatibility,
    decimal VoiceContinuityScore,
    decimal BalanceScore,
    decimal AverageAbsolutePitchChangeSemitones,
    IReadOnlyList<string> Recommendations);
