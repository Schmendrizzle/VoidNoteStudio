using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Analysis;

/// <summary>Analyzes a normalized track before arrangement.</summary>
public interface IShawzinCompatibilityAnalyzer
{
    ShawzinCompatibilityReport Analyze(MidiTrack track, Domain.Music.ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale);
}

/// <summary>Ranks every instrument-supported scale for a normalized track.</summary>
public interface IShawzinScaleAnalyzer
{
    IReadOnlyList<ShawzinScaleCandidate> Analyze(MidiTrack track, ShawzinDefinition instrument);
}

/// <summary>Scores a configurable range of transpositions without applying one.</summary>
public interface IShawzinTranspositionAnalyzer
{
    IReadOnlyList<ShawzinTranspositionCandidate> Analyze(MidiTrack track, Domain.Music.ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale, int minimumSemitones = -12, int maximumSemitones = 12);
}
