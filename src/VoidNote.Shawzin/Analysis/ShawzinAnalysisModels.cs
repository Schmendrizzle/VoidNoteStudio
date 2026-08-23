using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Analysis;

/// <summary>Contains explainable pitch, timing, chord and density compatibility metrics.</summary>
public sealed record ShawzinCompatibilityReport(
    int TotalNotes,
    int DirectlyPlayableNotes,
    int UnsupportedNotes,
    int OutsideRangeNotes,
    int OctaveFixableNotes,
    int TimingConflicts,
    int PolyphonyConflicts,
    int ChordConflicts,
    int QuantizationCollisions,
    int ExcessiveDensityWindows,
    int OverallScore,
    int PitchSubstitutionNotes,
    int DroppedNotes,
    decimal MeanPitchErrorSemitones,
    int MaximumPitchErrorSemitones,
    decimal ExpectedChangeRatePercent)
{
    public decimal DirectlyPlayablePercent => Percentage(DirectlyPlayableNotes);
    public decimal OctaveFixablePercent => Percentage(OctaveFixableNotes);
    public decimal UnsupportedPercent => Percentage(UnsupportedNotes + OutsideRangeNotes);
    private decimal Percentage(int count) => TotalNotes == 0 ? 100m : decimal.Round(count * 100m / TotalNotes, 1);
}

/// <summary>Ranks one supported scale by direct coverage and pitch-class fit.</summary>
public sealed record ShawzinScaleCandidate(
    ShawzinScale Scale,
    string DisplayName,
    int DirectlyPlayableNotes,
    int TotalNotes,
    decimal DirectCoveragePercent,
    decimal PitchClassFitPercent,
    decimal SuitabilityScore,
    int OctaveFixableNotes,
    int NotPlayableNotes,
    int PitchSubstitutionNotes,
    decimal MeanPitchErrorSemitones,
    int MaximumPitchErrorSemitones);

/// <summary>Scores one non-applied semitone transposition.</summary>
public sealed record ShawzinTranspositionCandidate(
    int Semitones,
    int DirectlyPlayableNotes,
    int OctaveFixableNotes,
    int LostNotes,
    int Conflicts,
    decimal Score,
    int PitchSubstitutionNotes,
    int DroppedNotes,
    decimal MeanPitchErrorSemitones,
    int MaximumPitchErrorSemitones);
