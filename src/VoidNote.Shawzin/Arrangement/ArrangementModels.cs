using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Arrangement;

[Flags]
/// <summary>Identifies independently configurable arrangement policies.</summary>
public enum ArrangementStrategy
{
    Strict = 1,
    ClosestPitch = 2,
    PreserveMelody = 4,
    OctaveShift = 8,
    DropLowest = 16,
    DropHighest = 32,
    Arpeggiate = 64,
    Simplify = 128,
}

/// <summary>Identifies an auditable arrangement transformation or conflict.</summary>
public enum ArrangementChangeType
{
    Transposed,
    OctaveShift,
    PitchSubstitution,
    DroppedNote,
    Arpeggiated,
    Quantized,
    ConflictUnresolved,
}

/// <summary>Configures one deterministic arrangement run.</summary>
public sealed record ArrangementOptions
{
    public ShawzinScale Scale { get; init; } = ShawzinScale.Chromatic;
    public ArrangementStrategy Strategies { get; init; } = ArrangementStrategy.Strict;
    public bool AllowTransposition { get; init; }
    public int TranspositionSemitones { get; init; }
    public decimal QuantizationStepSeconds { get; init; } = 0.0625m;
    public decimal MaximumArpeggioSpreadSeconds { get; init; } = 0.1875m;
    public int MaximumNotesPerSecond { get; init; } = 12;
}

/// <summary>Traces one source note through a transformation or unresolved conflict.</summary>
public sealed record ArrangementChange(
    Guid SourceEventId,
    int SourcePitch,
    int? TargetPitch,
    MusicalTime OriginalTime,
    MusicalTime? NewTime,
    ArrangementChangeType ChangeType,
    string Reason,
    ArrangementStrategy Strategy);

/// <summary>Summarizes quantization error and collisions.</summary>
public sealed record ArrangementTimingMetrics(
    decimal MaximumErrorSeconds,
    decimal AverageErrorSeconds,
    int CollidedEvents);

/// <summary>Contains every transformation and aggregate arrangement metrics.</summary>
public sealed record ArrangementReport(
    IReadOnlyList<ArrangementChange> Changes,
    ArrangementTimingMetrics Timing,
    int SourceNoteCount,
    int OutputEventCount,
    int OutputNoteCount)
{
    public bool HasUnresolvedConflicts => Changes.Any(value => value.ChangeType == ArrangementChangeType.ConflictUnresolved);
}

/// <summary>Contains a complete track or an explicit unresolved report.</summary>
public sealed record ShawzinArrangementResult(ShawzinTrack? Track, ArrangementReport Report)
{
    public bool IsSuccess => Track is not null && !Report.HasUnresolvedConflicts;
}

/// <summary>Converts a normalized MIDI track into one Shawzin track.</summary>
public interface IShawzinArranger
{
    ShawzinArrangementResult Arrange(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ArrangementOptions options);
}
