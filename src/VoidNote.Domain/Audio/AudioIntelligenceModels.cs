using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Domain.Audio;

/// <summary>Known stem roles. <see cref="Custom"/> keeps engine-specific extensions possible.</summary>
public enum StemType { Vocals, Bass, Drums, Other, Guitar, Piano, Strings, BackingVocals, Custom }

/// <summary>Identifies the non-destructive input window used for an AI operation.</summary>
public sealed record AudioProcessingSource
{
    public required Guid AudioSourceId { get; init; }
    public Guid? AudioRegionId { get; init; }
    public Guid? StemId { get; init; }
    /// <summary>Offset within the immutable input file.</summary>
    public AbsoluteTime SourceOffset { get; init; } = AbsoluteTime.Zero;
    /// <summary>Offset on the project master timeline.</summary>
    public AbsoluteTime StartOffset { get; init; } = AbsoluteTime.Zero;
    public required AbsoluteTime Duration { get; init; }
}

/// <summary>Describes how a derived audio asset was produced.</summary>
public sealed record AudioAssetProvenance
{
    public required Guid SourceAudioId { get; init; }
    public Guid? SourceRegionId { get; init; }
    public required string Engine { get; init; }
    public required string EngineVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>A separated, file-backed and independently playable derived asset.</summary>
public sealed class Stem : ProjectAsset
{
    public required Guid StemSetId { get; init; }
    public required Guid SourceAudioId { get; init; }
    public required Guid AudioSourceId { get; init; }
    public required StemType Type { get; init; }
    public string? CustomType { get; init; }
    public required string Engine { get; init; }
    public required string EngineVersion { get; init; }
    public Dictionary<string, string> ProcessingSettings { get; init; } = [];
    public required AbsoluteTime Duration { get; init; }
    public AbsoluteTime StartOffset { get; init; } = AbsoluteTime.Zero;
    public required AudioAssetProvenance Provenance { get; init; }
}

/// <summary>Groups all derived stems from one immutable source or region.</summary>
public sealed class StemSet : ProjectItem
{
    public required AudioProcessingSource Source { get; init; }
    public List<Stem> StemTracks { get; init; } = [];
    public required string SeparationEngine { get; init; }
    public required string EngineVersion { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Settings { get; init; } = [];
    public Dictionary<string, string> ProcessingMetadata { get; init; } = [];
}

/// <summary>Coarse confidence class derived from configurable thresholds.</summary>
public enum NoteConfidenceLevel { Low, Medium, High }

/// <summary>Tracks whether a detection is untouched, edited or explicitly accepted.</summary>
public enum DetectionEditStatus { OriginalDetection, UserModified, UserConfirmed }

/// <summary>Persistent origin information for an automatically detected note.</summary>
public sealed record AudioNoteProvenance
{
    public required Guid SourceAudioId { get; init; }
    public Guid? SourceStemId { get; init; }
    public required string Engine { get; init; }
    public required string EngineVersion { get; init; }
    public required decimal RawConfidence { get; init; }
    public required NoteConfidenceLevel ConfidenceLevel { get; init; }
    public DetectionEditStatus EditStatus { get; init; } = DetectionEditStatus.OriginalDetection;
    public required MusicalTime OriginalStart { get; init; }
    public required MusicalTime OriginalDuration { get; init; }
}

public enum TranscriptionMode { Auto, Monophonic, Polyphonic }
public enum ConfidenceFilterMode { KeepAll, HideLow, RemoveLow, MinimumThreshold }
public enum TranscriptionQuantization { None, Quarter, Eighth, Sixteenth, ThirtySecond, EighthTriplet, SixteenthTriplet }

/// <summary>Settings persisted with an audio transcription result.</summary>
public sealed record AudioTranscriptionSettings
{
    public TranscriptionMode Mode { get; init; } = TranscriptionMode.Auto;
    public decimal HighConfidenceThreshold { get; init; } = 0.85m;
    public decimal MediumConfidenceThreshold { get; init; } = 0.60m;
    public ConfidenceFilterMode ConfidenceFilter { get; init; } = ConfidenceFilterMode.KeepAll;
    public decimal MinimumConfidence { get; init; }
    public TranscriptionQuantization Quantization { get; init; }
    public AbsoluteTime MinimumNoteDuration { get; init; } = new(0.04m);
    public AbsoluteTime MergeGap { get; init; } = new(0.03m);
    public bool RemoveGhostNotes { get; init; }
    public bool MergeAdjacentNotes { get; init; }
    public bool DetectDuplicates { get; init; } = true;
    public bool MarkPitchOutliers { get; init; } = true;
}

public enum TranscriptionChangeType { ConfidenceFiltered, GhostNoteRemoved, NotesMerged, DuplicateRemoved, PitchOutlierMarked, PolyphonyReduced, Quantized }

public sealed record TranscriptionChange(Guid? NoteId, TranscriptionChangeType Type, string Reason);

/// <summary>Auditable metrics and cleanup decisions for one transcription.</summary>
public sealed class AudioTranscriptionReport : ProjectItem
{
    public required Guid MidiTrackId { get; init; }
    public required AudioProcessingSource Source { get; init; }
    public int DetectedNotes { get; init; }
    public int KeptNotes { get; init; }
    public int DiscardedNotes { get; init; }
    public decimal AverageConfidence { get; init; }
    public int HighConfidenceCount { get; init; }
    public int MediumConfidenceCount { get; init; }
    public int LowConfidenceCount { get; init; }
    public required AbsoluteTime AnalyzedDuration { get; init; }
    public int? MinimumPitch { get; init; }
    public int? MaximumPitch { get; init; }
    public decimal NoteDensityPerSecond { get; init; }
    public required string Engine { get; init; }
    public required string EngineVersion { get; init; }
    public required TimeSpan ProcessingDuration { get; init; }
    public required AudioTranscriptionSettings Settings { get; init; }
    public List<TranscriptionChange> Changes { get; init; } = [];
}
