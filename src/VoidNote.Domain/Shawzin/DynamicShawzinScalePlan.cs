using VoidNote.Domain.Music;

namespace VoidNote.Domain.Shawzin;

/// <summary>Distinguishes a shareable fixed-scale song from extended GameBridge-only playback.</summary>
public enum ShawzinArrangementMode
{
    ShareCode,
    DynamicIngame,
}

/// <summary>Changes the active in-game scale through normal Scale Select key presses.</summary>
public sealed record ShawzinScaleChangeEvent
{
    public ShawzinScaleChangeEvent(Guid id, AbsoluteTime timestamp, ShawzinScale sourceScale, ShawzinScale targetScale,
        int requiredScaleKeyPressCount, string reason, decimal benefitScore, decimal availableWindowSeconds,
        decimal requiredWindowSeconds, bool isTimingSafe)
    {
        if (id == Guid.Empty) throw new ArgumentException("A scale-change event ID cannot be empty.", nameof(id));
        if (!Enum.IsDefined(sourceScale)) throw new ArgumentOutOfRangeException(nameof(sourceScale));
        if (!Enum.IsDefined(targetScale)) throw new ArgumentOutOfRangeException(nameof(targetScale));
        if (requiredScaleKeyPressCount is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(requiredScaleKeyPressCount));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(availableWindowSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredWindowSeconds);
        Id = id;
        Timestamp = timestamp;
        SourceScale = sourceScale;
        TargetScale = targetScale;
        RequiredScaleKeyPressCount = requiredScaleKeyPressCount;
        Reason = reason;
        BenefitScore = benefitScore;
        AvailableWindowSeconds = availableWindowSeconds;
        RequiredWindowSeconds = requiredWindowSeconds;
        IsTimingSafe = isTimingSafe;
    }

    public Guid Id { get; }
    public AbsoluteTime Timestamp { get; }
    public ShawzinScale SourceScale { get; }
    public ShawzinScale TargetScale { get; }
    public int RequiredScaleKeyPressCount { get; }
    public string Reason { get; }
    public decimal BenefitScore { get; }
    public decimal AvailableWindowSeconds { get; }
    public decimal RequiredWindowSeconds { get; }
    public bool IsTimingSafe { get; }
}

/// <summary>Associates a physical strike with the scale needed to reconstruct its expected pitch.</summary>
public sealed record DynamicShawzinNoteEvent(
    ShawzinEvent Event,
    ShawzinScale Scale,
    IReadOnlyList<int> SourcePitches,
    IReadOnlyList<int> ResultingPitches);

/// <summary>Describes one stable musical section selected by the scale planner.</summary>
public sealed record DynamicShawzinSection(
    int Index,
    AbsoluteTime Start,
    AbsoluteTime End,
    ShawzinScale Scale,
    int SourceNoteCount,
    decimal AvailablePauseBeforeSeconds,
    bool HasSafeChangeWindow,
    decimal SimilarityScore);

/// <summary>Comparable musical quality measurements for fixed and dynamic arrangements.</summary>
public sealed record DynamicShawzinQualityMetrics(
    int SourceNoteCount,
    int OutputNoteCount,
    int ExactPitchCount,
    int PitchSubstitutionCount,
    int OctaveShiftCount,
    int DroppedNoteCount,
    decimal MeanPitchErrorSemitones,
    decimal PlayabilityPercent,
    decimal MusicalSimilarityPercent,
    int ScaleChangeCount,
    int TotalScaleKeyPresses);

/// <summary>An extended playback plan that is deliberately separate from the classic song-code model.</summary>
public sealed record DynamicShawzinScalePlan(
    ShawzinArrangementMode Mode,
    ShawzinScale RequiredInitialScale,
    IReadOnlyList<DynamicShawzinNoteEvent> NoteEvents,
    IReadOnlyList<ShawzinScaleChangeEvent> ScaleChangeEvents,
    IReadOnlyList<DynamicShawzinSection> Sections,
    DynamicShawzinQualityMetrics Metrics,
    ShawzinTrack FixedScaleFallback,
    ShawzinScale FixedScale,
    DynamicShawzinQualityMetrics FixedScaleMetrics)
{
    /// <summary>Only a plan that actually uses one scale may be represented by a classic song code.</summary>
    public bool CanExportClassicShareCode => Mode == ShawzinArrangementMode.ShareCode && ScaleChangeEvents.Count == 0;
}
