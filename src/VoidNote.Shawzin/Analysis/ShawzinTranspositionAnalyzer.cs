using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Analysis;

/// <summary>Scores semitone transpositions through the central pitch mapper.</summary>
public sealed class ShawzinTranspositionAnalyzer(IShawzinPitchMapper mapper) : IShawzinTranspositionAnalyzer
{
    private readonly IShawzinPitchMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    public IReadOnlyList<ShawzinTranspositionCandidate> Analyze(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale, int minimumSemitones = -12, int maximumSemitones = 12)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(timeline);
        if (minimumSemitones > maximumSemitones) throw new ArgumentException("The transposition range is reversed.");
        return Enumerable.Range(minimumSemitones, maximumSemitones - minimumSemitones + 1).Select(semitones =>
        {
            var results = track.Events.Select(note => PitchQuality.EvaluateTransposed(note.Pitch, semitones, _mapper, instrument, scale)).ToArray();
            var direct = results.Count(value => value.Kind == PitchQualityKind.Direct);
            var octave = results.Count(value => value.Kind == PitchQualityKind.Octave);
            var substitutions = results.Count(value => value.Kind == PitchQualityKind.Substitution);
            var dropped = results.Count(value => value.Kind == PitchQualityKind.Dropped);
            var lost = substitutions + dropped;
            var conflicts = track.Events.GroupBy(value => value.StartTime.Ticks).Count(group => group.Count() > 3);
            var meanError = results.Length == 0 ? 0m : results.Average(value => (decimal)value.Distance);
            var maximumError = results.Select(value => value.Distance).DefaultIfEmpty().Max();
            var quality = results.Length == 0 ? 100m : results.Sum(value => value.Quality) * 100m / results.Length;
            var conflictPenalty = conflicts * 100m / Math.Max(1, results.Length) * 0.15m;
            var score = Math.Clamp(quality - meanError * 1.5m - conflictPenalty, 0m, 100m);
            return new ShawzinTranspositionCandidate(semitones, direct, octave, lost, conflicts, decimal.Round(score, 1),
                substitutions, dropped, decimal.Round(meanError, 2), maximumError);
        }).OrderByDescending(value => value.Score)
          .ThenBy(value => Math.Abs(value.Semitones))
          .ThenBy(value => value.Semitones)
          .ToArray();
    }
}
