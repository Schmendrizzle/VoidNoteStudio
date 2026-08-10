using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Analysis;

/// <summary>Measures pitch, input and timestamp compatibility without transforming the source.</summary>
public sealed class ShawzinCompatibilityAnalyzer(IShawzinPitchMapper mapper) : IShawzinCompatibilityAnalyzer
{
    public const decimal TimestampSeconds = 0.0625m;
    public const int DenseNotesPerSecond = 12;
    private readonly IShawzinPitchMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    public ShawzinCompatibilityReport Analyze(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(instrument);
        var notes = track.Events.OrderBy(value => value.StartTime.Ticks).ThenBy(value => value.Pitch).ThenBy(value => value.Id).ToArray();
        var mappings = notes.Select(note => _mapper.Map(note.Pitch, instrument, scale)).ToArray();
        var direct = mappings.Count(value => value.Kind == ShawzinPitchMappingKind.Exact);
        var octave = mappings.Count(value => value.Kind == ShawzinPitchMappingKind.OctaveShiftable);
        var outside = mappings.Count(value => value.Kind == ShawzinPitchMappingKind.OutsideRange);
        var unsupported = mappings.Count(value => value.Kind == ShawzinPitchMappingKind.NotAvailable);

        var groups = notes.GroupBy(value => value.StartTime.Ticks).ToArray();
        var polyphony = groups.Count(group => group.Count() > 3);
        var chordConflicts = groups.Count(group => group.Count() > 1 && !CanFormChord(group.Select(note => _mapper.Map(note.Pitch, instrument, scale)).ToArray()));
        var quantizedGroups = groups.Select(group => new
        {
            SourceTick = group.Key,
            Timestamp = Quantize(timeline.ToAbsoluteTime(group.First().StartTime).Seconds),
        }).ToArray();
        var collisions = quantizedGroups.GroupBy(value => value.Timestamp).Sum(group => Math.Max(0, group.Count() - 1));
        var timing = collisions;
        var dense = CountDenseWindows(notes, timeline);

        var pitchScore = notes.Length == 0 ? 100m : (direct + octave * 0.75m) * 100m / notes.Length;
        var groupCount = Math.Max(1, groups.Length);
        var penalty = Math.Min(10m, timing * 10m / groupCount)
                    + Math.Min(10m, polyphony * 10m / groupCount)
                    + Math.Min(10m, chordConflicts * 10m / groupCount)
                    + Math.Min(5m, dense * 5m / groupCount);
        var score = (int)decimal.Round(Math.Clamp(pitchScore - penalty, 0m, 100m), 0, MidpointRounding.AwayFromZero);

        return new ShawzinCompatibilityReport(notes.Length, direct, unsupported, outside, octave, timing, polyphony, chordConflicts, collisions, dense, score);
    }

    internal static bool CanFormChord(IReadOnlyList<ShawzinPitchMappingResult> mappings)
    {
        if (mappings.Count is < 1 or > 3 || mappings.Any(value => value.Kind != ShawzinPitchMappingKind.Exact)) return false;
        return Search(mappings, 0, null, new HashSet<ShawzinString>());
    }

    internal static int Quantize(decimal seconds) => checked((int)decimal.Round(seconds / TimestampSeconds, 0, MidpointRounding.AwayFromZero));

    private static bool Search(IReadOnlyList<ShawzinPitchMappingResult> mappings, int index, ShawzinFret? frets, HashSet<ShawzinString> strings)
    {
        if (index == mappings.Count) return true;
        foreach (var candidate in mappings[index].Candidates)
        {
            if (frets is not null && candidate.Input.Frets != frets || !strings.Add(candidate.Input.String)) continue;
            if (Search(mappings, index + 1, frets ?? candidate.Input.Frets, strings)) return true;
            strings.Remove(candidate.Input.String);
        }
        return false;
    }

    private static int CountDenseWindows(IReadOnlyList<MusicalEvent> notes, ProjectTimeline timeline)
    {
        var times = notes.Select(value => timeline.ToAbsoluteTime(value.StartTime).Seconds).Order().ToArray();
        var count = 0;
        var left = 0;
        for (var right = 0; right < times.Length; right++)
        {
            while (times[right] - times[left] >= 1m) left++;
            if (right - left + 1 == DenseNotesPerSecond + 1) count++;
        }
        return count;
    }
}
