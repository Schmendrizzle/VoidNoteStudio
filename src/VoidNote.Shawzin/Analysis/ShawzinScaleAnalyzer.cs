using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Analysis;

/// <summary>Provides deterministic scale ranking from pitch-class and direct-pitch coverage.</summary>
public sealed class ShawzinScaleAnalyzer(IShawzinPitchMapper? mapper = null) : IShawzinScaleAnalyzer
{
    private readonly IShawzinPitchMapper _mapper = mapper ?? new ShawzinPitchMapper();

    public IReadOnlyList<ShawzinScaleCandidate> Analyze(MidiTrack track, ShawzinDefinition instrument)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(instrument);
        var usedClasses = track.Events.Select(value => value.Pitch % 12).Distinct().ToArray();
        return instrument.PlayProfile.Scales.Values.Select(scale =>
        {
            var mappings = track.Events.Select(value => PitchQuality.Evaluate(value.Pitch, _mapper, instrument, scale.Scale)).ToArray();
            var direct = mappings.Count(value => value.Kind == PitchQualityKind.Direct);
            var octave = mappings.Count(value => value.Kind == PitchQualityKind.Octave);
            var substitutions = mappings.Count(value => value.Kind == PitchQualityKind.Substitution);
            var dropped = mappings.Count(value => value.Kind == PitchQualityKind.Dropped);
            var coverage = track.Events.Count == 0 ? 100m : direct * 100m / track.Events.Count;
            var classFit = usedClasses.Length == 0 ? 100m : usedClasses.Count(scale.PitchClasses.Contains) * 100m / usedClasses.Length;
            var meanError = mappings.Length == 0 ? 0m : mappings.Average(value => (decimal)value.Distance);
            var maximumError = mappings.Select(value => value.Distance).DefaultIfEmpty().Max();
            var quality = mappings.Length == 0 ? 100m : mappings.Sum(value => value.Quality) * 100m / mappings.Length;
            var score = Math.Clamp(quality - meanError * 1.5m - dropped * 100m / Math.Max(1, mappings.Length) * 0.25m, 0m, 100m);
            return new ShawzinScaleCandidate(scale.Scale, scale.DisplayName, direct, track.Events.Count,
                decimal.Round(coverage, 1), decimal.Round(classFit, 1), decimal.Round(score, 1), octave,
                substitutions + dropped, substitutions, decimal.Round(meanError, 2), maximumError);
        }).OrderByDescending(value => value.SuitabilityScore)
          .ThenByDescending(value => value.DirectCoveragePercent)
          .ThenBy(value => value.Scale)
          .ToArray();
    }
}
