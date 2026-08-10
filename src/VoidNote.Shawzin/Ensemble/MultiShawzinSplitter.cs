using System.Security.Cryptography;
using System.Text;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>Greedily separates voices with deterministic salience, continuity, overlap and balance costs.</summary>
public sealed class MultiShawzinSplitter(VoiceSalienceAnalyzer salienceAnalyzer) : IMultiShawzinSplitter
{
    private readonly VoiceSalienceAnalyzer _salience = salienceAnalyzer ?? throw new ArgumentNullException(nameof(salienceAnalyzer));

    public MultiShawzinSplitResult Split(IReadOnlyList<MidiTrack> sourceTracks, MultiShawzinSplitOptions options)
    {
        ArgumentNullException.ThrowIfNull(sourceTracks);
        ArgumentNullException.ThrowIfNull(options);
        if (sourceTracks.Count == 0) throw new ArgumentException("At least one source track is required.", nameof(sourceTracks));
        if (options.ShawzinCount < 2) throw new ArgumentOutOfRangeException(nameof(options), "At least two Shawzins are required.");
        if (options.Preferences.Any(value => value.TrackIndex < 0 || value.TrackIndex >= options.ShawzinCount))
            throw new ArgumentOutOfRangeException(nameof(options), "A voice preference references an unavailable track.");

        var source = sourceTracks.SelectMany(track => track.Events.Select(note => new SourceNote(track, note)))
            .OrderBy(value => value.Note.StartTime.Ticks).ThenBy(value => value.Note.Pitch).ThenBy(value => value.Note.Id).ToArray();
        var names = CreatorNames(options.Strategy, options.ShawzinCount);
        var voiceIds = Enumerable.Range(0, options.ShawzinCount).Select(index => StableId(sourceTracks, options, index)).ToArray();
        var states = Enumerable.Range(0, options.ShawzinCount).Select(index => new VoiceState(index, voiceIds[index], names[index], options.ShawzinCount)).ToArray();
        var assignments = new List<SplitAssignment>(source.Length);
        MusicalEvent? previousMelody = null;
        MusicalEvent? previousBass = null;

        foreach (var group in source.GroupBy(value => value.Note.StartTime.Ticks))
        {
            var local = group.Select(value => value.Note).OrderBy(value => value.Pitch).ThenBy(value => value.Id).ToArray();
            var reserved = new HashSet<int>();
            foreach (var item in group.OrderBy(value => Priority(value.Note, local, options.Strategy, previousMelody, previousBass)).ThenBy(value => value.Note.Id))
            {
                var melody = _salience.MelodyScore(item.Note, local, previousMelody);
                var bass = _salience.BassScore(item.Note, local, previousBass);
                var candidates = reserved.Count < states.Length ? states.Where(state => !reserved.Contains(state.Index)) : states;
                var ranked = candidates.Select(state => Score(state, item.Note, local, melody, bass, options.Strategy, source.Length, reserved.Contains(state.Index)))
                    .OrderByDescending(value => value.Score).ThenBy(value => value.State.Index).ToArray();
                var selected = ranked[0];
                selected.State.Add(item.Note);
                reserved.Add(selected.State.Index);
                if (selected.State.Index == 0 && melody >= 0.5m) previousMelody = item.Note;
                if (options.Strategy == MultiShawzinSplitStrategy.MelodyBass && selected.State.Index == 1 && bass >= 0.45m) previousBass = item.Note;
                assignments.Add(new SplitAssignment(item.Track.Id, item.Note.Id, item.Note.Pitch, item.Note.StartTime,
                    selected.State.Id, selected.State.Name, options.Strategy, selected.Score, Confidence(selected.Score, ranked), selected.Reason));
            }
        }

        var voices = states.Select(state => new SplitVoice(state.Id, state.Name, new MidiTrack
        {
            Id = state.Id,
            Name = state.Name,
            Events = [.. state.Notes.OrderBy(value => value.StartTime.Ticks).ThenBy(value => value.Pitch).ThenBy(value => value.Id)],
        }, options.Preferences.FirstOrDefault(value => value.TrackIndex == state.Index))).ToArray();
        var metrics = Metrics(source.Length, states, assignments);
        return new MultiShawzinSplitResult(voices, new MultiShawzinSplitReport(options.Strategy, assignments, metrics, []));
    }

    private static ScoredVoice Score(VoiceState state, MusicalEvent note, IReadOnlyList<MusicalEvent> local, decimal melody, decimal bass,
        MultiShawzinSplitStrategy strategy, int sourceCount, bool simultaneousTrackUsed)
    {
        var continuity = state.Last is null ? 0.5m : 1m - Math.Min(1m, Math.Abs(note.Pitch - state.Last.Pitch) / 24m);
        var temporal = state.Last is null ? 0.5m : 1m - Math.Min(1m, Math.Abs(note.StartTime.Ticks - state.Last.StartTime.Ticks - state.Last.Duration.Ticks) / 3840m);
        var overlap = state.Last is not null && state.Last.StartTime.Ticks + state.Last.Duration.Ticks > note.StartTime.Ticks ? 1m : 0m;
        var targetLoad = sourceCount / (decimal)Math.Max(1, state.TotalVoices);
        var balance = 1m - Math.Min(1m, state.Notes.Count / Math.Max(1m, targetLoad));
        var registerTarget = state.TotalVoices == 1 ? 0.5m : 1m - state.Index / (decimal)(state.TotalVoices - 1);
        var pitchPosition = local.Count == 1 || local[^1].Pitch == local[0].Pitch ? 0.5m : (note.Pitch - local[0].Pitch) / (decimal)(local[^1].Pitch - local[0].Pitch);
        var register = 1m - Math.Abs(pitchPosition - registerTarget);
        decimal role = 0.5m;
        string roleReason = "voice continuity";
        if (strategy is MultiShawzinSplitStrategy.MelodyHarmony or MultiShawzinSplitStrategy.MaximumRecognition && state.Index == 0)
        { role = melody; roleReason = "melody salience"; }
        else if (strategy == MultiShawzinSplitStrategy.MelodyBass)
        {
            if (state.Index == 0) { role = melody; roleReason = "lead salience"; }
            else if (state.Index == 1) { role = bass; roleReason = "bass continuity and register"; }
        }
        else if (strategy == MultiShawzinSplitStrategy.RegisterSplit) { role = register; roleReason = "pitch register"; }
        else if (strategy == MultiShawzinSplitStrategy.CreatorMultitrack) { role = (continuity + temporal) / 2m; roleReason = "self-contained creator voice"; }
        else if (strategy == MultiShawzinSplitStrategy.MinimalNoteLoss) { role = (1m - overlap + balance) / 2m; roleReason = "available capacity"; }

        var score = 0.34m * role + 0.26m * continuity + 0.14m * temporal + 0.12m * register + 0.14m * balance
            - 0.34m * overlap - (simultaneousTrackUsed ? 0.22m : 0m);
        return new ScoredVoice(state, decimal.Round(score, 4, MidpointRounding.AwayFromZero),
            $"{roleReason}; continuity {continuity:P0}; temporal {temporal:P0}; balance {balance:P0}; overlap penalty {overlap:P0}");
    }

    private decimal Priority(MusicalEvent note, IReadOnlyList<MusicalEvent> local, MultiShawzinSplitStrategy strategy, MusicalEvent? melody, MusicalEvent? bass) =>
        strategy == MultiShawzinSplitStrategy.MelodyBass
            ? -Math.Max(_salience.MelodyScore(note, local, melody), _salience.BassScore(note, local, bass))
            : -_salience.MelodyScore(note, local, melody);

    private static decimal Confidence(decimal selected, IReadOnlyList<ScoredVoice> ranked) => ranked.Count < 2
        ? 1m
        : decimal.Round(Math.Clamp(0.5m + (selected - ranked[1].Score) / 2m, 0m, 1m), 3, MidpointRounding.AwayFromZero);

    private static SplitMetrics Metrics(int sourceCount, IReadOnlyList<VoiceState> states, IReadOnlyList<SplitAssignment> assignments)
    {
        var assigned = assignments.Count(value => !value.IsDropped);
        var duplicates = assignments.Count(value => value.IsDuplicate);
        var dropped = sourceCount - assignments.Select(value => value.SourceEventId).Distinct().Count();
        var distribution = states.Select(value => assigned == 0 ? 0m : decimal.Round(value.Notes.Count * 100m / assigned, 1)).ToArray();
        var ideal = states.Count == 0 ? 0m : 100m / states.Count;
        var meanDeviation = distribution.Length == 0 ? 0m : distribution.Average(value => Math.Abs(value - ideal));
        var balance = decimal.Round(Math.Clamp(100m - meanDeviation * states.Count / 2m, 0m, 100m), 1);
        var intervals = states.SelectMany(state => state.Notes.Zip(state.Notes.Skip(1), (a, b) => Math.Abs(a.Pitch - b.Pitch))).ToArray();
        var continuity = intervals.Length == 0 ? 100m : decimal.Round(intervals.Average(value => Math.Max(0m, 100m - value * 100m / 24m)), 1);
        return new SplitMetrics(sourceCount, assigned, dropped, duplicates, sourceCount == 0 ? 0m : decimal.Round(dropped * 100m / sourceCount, 2), continuity, balance, distribution);
    }

    private static IReadOnlyList<string> CreatorNames(MultiShawzinSplitStrategy strategy, int count)
    {
        var names = strategy switch
        {
            MultiShawzinSplitStrategy.MelodyBass => new[] { "Lead", "Bass", "Harmony 1", "Harmony 2" },
            MultiShawzinSplitStrategy.RegisterSplit => new[] { "Upper Register", "Middle Register", "Lower Register", "Foundation" },
            MultiShawzinSplitStrategy.CreatorMultitrack => new[] { "Lead", "Counter Melody", "Harmony", "Bass" },
            _ => new[] { "Lead", "Harmony 1", "Harmony 2", "Bass" },
        };
        return Enumerable.Range(0, count).Select(index => index < names.Length ? names[index] : $"Harmony {index}").ToArray();
    }

    private static Guid StableId(IReadOnlyList<MidiTrack> tracks, MultiShawzinSplitOptions options, int index)
    {
        var value = $"{string.Join(',', tracks.Select(track => track.Id).Order())}|{options.Strategy}|{options.ShawzinCount}|{index}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    }

    private sealed record SourceNote(MidiTrack Track, MusicalEvent Note);
    private sealed record ScoredVoice(VoiceState State, decimal Score, string Reason);
    private sealed class VoiceState(int index, Guid id, string name, int totalVoices)
    {
        public int Index { get; } = index;
        public Guid Id { get; } = id;
        public string Name { get; } = name;
        public List<MusicalEvent> Notes { get; } = [];
        public MusicalEvent? Last => Notes.Count == 0 ? null : Notes[^1];
        public int TotalVoices { get; } = totalVoices;
        public void Add(MusicalEvent note) => Notes.Add(note);
    }
}
