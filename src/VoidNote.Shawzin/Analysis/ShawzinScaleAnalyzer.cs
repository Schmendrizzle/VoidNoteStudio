using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Analysis;

/// <summary>Provides deterministic scale ranking from pitch-class and direct-pitch coverage.</summary>
public sealed class ShawzinScaleAnalyzer : IShawzinScaleAnalyzer
{
    public IReadOnlyList<ShawzinScaleCandidate> Analyze(MidiTrack track, ShawzinDefinition instrument)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(instrument);
        var usedClasses = track.Events.Select(value => value.Pitch % 12).Distinct().ToArray();
        return instrument.PlayProfile.Scales.Values.Select(scale =>
        {
            var pitches = scale.Positions.Select(value => value.Pitch).ToHashSet();
            var direct = track.Events.Count(value => pitches.Contains(value.Pitch));
            var coverage = track.Events.Count == 0 ? 100m : direct * 100m / track.Events.Count;
            var classFit = usedClasses.Length == 0 ? 100m : usedClasses.Count(scale.PitchClasses.Contains) * 100m / usedClasses.Length;
            var complexityBonus = scale.Scale == ShawzinScale.Chromatic ? 0m : 5m;
            var score = Math.Min(100m, coverage * 0.7m + classFit * 0.3m + complexityBonus);
            return new ShawzinScaleCandidate(scale.Scale, scale.DisplayName, direct, track.Events.Count,
                decimal.Round(coverage, 1), decimal.Round(classFit, 1), decimal.Round(score, 1));
        }).OrderByDescending(value => value.SuitabilityScore)
          .ThenByDescending(value => value.DirectCoveragePercent)
          .ThenBy(value => value.Scale)
          .ToArray();
    }
}
