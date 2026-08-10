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
            var results = track.Events.Select(note => note.Pitch + semitones is < 0 or > 127
                ? null
                : _mapper.Map(note.Pitch + semitones, instrument, scale)).ToArray();
            var direct = results.Count(value => value?.Kind == ShawzinPitchMappingKind.Exact);
            var octave = results.Count(value => value?.Kind == ShawzinPitchMappingKind.OctaveShiftable);
            var lost = results.Length - direct - octave;
            var conflicts = track.Events.GroupBy(value => value.StartTime.Ticks).Count(group => group.Count() > 3);
            var score = results.Length == 0 ? 100m : Math.Clamp((direct + octave * 0.75m) * 100m / results.Length - conflicts * 5m, 0m, 100m);
            return new ShawzinTranspositionCandidate(semitones, direct, octave, lost, conflicts, decimal.Round(score, 1));
        }).OrderByDescending(value => value.Score)
          .ThenBy(value => Math.Abs(value.Semitones))
          .ThenBy(value => value.Semitones)
          .ToArray();
    }
}
