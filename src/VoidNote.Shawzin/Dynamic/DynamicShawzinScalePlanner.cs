using System.Security.Cryptography;
using System.Text;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;

namespace VoidNote.Shawzin.Dynamic;

/// <summary>Data-driven thresholds and physical timing assumptions for dynamic scale planning.</summary>
public sealed record DynamicShawzinScalePlanningSettings
{
    public ShawzinScale? InitialScale { get; init; }
    public decimal PhraseGapSeconds { get; init; } = 0.5m;
    public decimal MinimumSectionDurationSeconds { get; init; } = 0.75m;
    public int MinimumNotesBeforeChange { get; init; } = 3;
    public decimal ScaleChangeCost { get; init; } = 4m;
    public decimal ScaleKeyPressCost { get; init; } = 0.35m;
    public decimal ImprovementThreshold { get; init; } = 3m;
    public int MinimumPitchErrorsPrevented { get; init; } = 2;
    public int MinimumSubstitutionsPrevented { get; init; } = 2;
    public decimal ScaleKeyPressDurationSeconds { get; init; } = 0.035m;
    public decimal ScaleKeyReleaseDelaySeconds { get; init; } = 0.025m;
    public decimal MinimumGapBeforeNextNoteSeconds { get; init; } = 0.05m;
    public ArrangementStrategy ArrangementStrategies { get; init; } = ArrangementStrategy.ClosestPitch |
        ArrangementStrategy.OctaveShift | ArrangementStrategy.PreserveMelody | ArrangementStrategy.Arpeggiate | ArrangementStrategy.Simplify;
}

public interface IDynamicShawzinScalePlanner
{
    DynamicShawzinScalePlan Plan(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument,
        IReadOnlyCollection<ShawzinScale> allowedScales, DynamicShawzinScalePlanningSettings settings);
}

/// <summary>Plans stable phrase-scale sections with deterministic dynamic programming and safe transition windows.</summary>
public sealed class DynamicShawzinScalePlanner(IShawzinArranger arranger) : IDynamicShawzinScalePlanner
{
    private readonly IShawzinArranger _arranger = arranger ?? throw new ArgumentNullException(nameof(arranger));

    public DynamicShawzinScalePlan Plan(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument,
        IReadOnlyCollection<ShawzinScale> allowedScales, DynamicShawzinScalePlanningSettings settings)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(allowedScales);
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        var scales = allowedScales.Distinct().Where(instrument.Scales.ContainsKey).OrderBy(value => (int)value).ToArray();
        if (scales.Length == 0) throw new ArgumentException("At least one instrument-supported scale must be allowed.", nameof(allowedScales));
        if (settings.InitialScale is { } initial && !scales.Contains(initial))
            throw new ArgumentException("The configured initial scale must be allowed and supported.", nameof(settings));

        var notes = track.Events.OrderBy(value => value.StartTime.Ticks).ThenBy(value => value.Pitch).ThenBy(value => value.Id).ToArray();
        var phrases = CreatePhrases(notes, timeline, settings.PhraseGapSeconds);
        var fixedCandidates = scales.Select(scale => Arrange(track, timeline, instrument, scale, settings)).ToArray();
        var bestFixed = fixedCandidates.OrderByDescending(value => value.Score).ThenBy(value => value.Scale).First();
        if (notes.Length == 0 || phrases.Count == 0)
            return EmptyPlan(track, bestFixed);

        var candidates = phrases.Select(phrase => scales.ToDictionary(scale => scale,
            scale => Arrange(Track(track, phrase.Notes), timeline, instrument, scale, settings))).ToArray();
        var startScale = settings.InitialScale ?? candidates[0].Values.OrderByDescending(value => value.Score).ThenBy(value => value.Scale).First().Scale;
        var states = new Dictionary<ShawzinScale, PathState>
        {
            [startScale] = new(candidates[0][startScale].Score, [startScale], [])
        };

        for (var sectionIndex = 1; sectionIndex < phrases.Count; sectionIndex++)
        {
            var next = new Dictionary<ShawzinScale, PathState>();
            foreach (var target in scales)
            {
                foreach (var (source, state) in states)
                {
                    var targetCandidate = candidates[sectionIndex][target];
                    var transition = EvaluateTransition(phrases[sectionIndex], source, target,
                        candidates[sectionIndex][source], targetCandidate, settings);
                    if (!transition.Allowed) continue;
                    var score = state.Score + targetCandidate.Score - transition.Penalty;
                    var proposed = new PathState(score, [.. state.Scales, target], [.. state.Transitions, transition]);
                    if (!next.TryGetValue(target, out var existing) || IsBetter(proposed, existing)) next[target] = proposed;
                }
            }
            states = next;
        }

        var selected = states.Values.OrderByDescending(value => value.Score)
            .ThenBy(value => value.Transitions.Count(value => value.IsChange))
            .ThenBy(value => string.Join(',', value.Scales.Select(scale => (int)scale)), StringComparer.Ordinal).First();
        var fixedPhraseScore = phrases.Select((_, index) => candidates[index][bestFixed.Scale].Score).Sum();
        if (selected.Score - fixedPhraseScore < settings.ImprovementThreshold)
            selected = new(fixedPhraseScore, Enumerable.Repeat(bestFixed.Scale, phrases.Count).ToArray(),
                Enumerable.Range(1, phrases.Count - 1).Select(index => Transition.Unchanged(phrases[index])).ToArray());

        return BuildPlan(track, phrases, candidates, selected, bestFixed, settings);
    }

    private DynamicShawzinScalePlan BuildPlan(MidiTrack sourceTrack, IReadOnlyList<Phrase> phrases,
        IReadOnlyList<Dictionary<ShawzinScale, Candidate>> candidates, PathState selected, Candidate fixedCandidate,
        DynamicShawzinScalePlanningSettings settings)
    {
        var noteEvents = new List<DynamicShawzinNoteEvent>();
        var sections = new List<DynamicShawzinSection>();
        var changes = new List<ShawzinScaleChangeEvent>();
        var reports = new List<ArrangementReport>();
        for (var index = 0; index < phrases.Count; index++)
        {
            var phrase = phrases[index];
            var scale = selected.Scales[index];
            var candidate = candidates[index][scale];
            reports.Add(candidate.Result.Report);
            var sourceByTime = phrase.Notes.GroupBy(value => QuantizedSeconds(value, phrase.Timeline)).ToDictionary(value => value.Key, value => value.ToArray());
            foreach (var value in candidate.Result.Track?.ShawzinEvents ?? [])
            {
                var original = sourceByTime.OrderBy(pair => Math.Abs(pair.Key - value.Position.Seconds)).First().Value;
                var resulting = candidate.Result.Track!.Events.Where(note => note.StartTime == phrase.Timeline.ToMusicalTime(value.Position)).Select(note => note.Pitch).ToArray();
                if (resulting.Length == 0) resulting = original.Select(note => note.Pitch).ToArray();
                noteEvents.Add(new(value, scale, original.Select(note => note.Pitch).ToArray(), resulting));
            }
            var transition = index == 0 ? null : selected.Transitions[index - 1];
            sections.Add(new(index, phrase.Start, phrase.End, scale, phrase.Notes.Count, phrase.PauseBefore,
                transition?.TimingSafe ?? true, candidate.Result.Report.MusicalSimilarity.OverallScore));
            if (transition is { IsChange: true })
            {
                var timestamp = new AbsoluteTime(Math.Max(phrase.PreviousEnd.Seconds,
                    phrase.Start.Seconds - transition.RequiredWindowSeconds));
                changes.Add(new(StableId(sourceTrack.Id, index, transition.Source, transition.Target), timestamp,
                    transition.Source, transition.Target, transition.Presses,
                    $"Prevents {transition.PitchErrorsPrevented} semitones of pitch error and {transition.SubstitutionsPrevented} substitutions in section {index + 1}.",
                    decimal.Round(transition.Benefit, 2), phrase.PauseBefore, transition.RequiredWindowSeconds, transition.TimingSafe));
            }
        }
        noteEvents.Sort((left, right) => left.Event.Position.Seconds.CompareTo(right.Event.Position.Seconds));
        var metrics = Aggregate(reports, changes);
        var mode = changes.Count == 0 && selected.Scales.Distinct().Count() == 1
            ? ShawzinArrangementMode.ShareCode : ShawzinArrangementMode.DynamicIngame;
        return new(mode, selected.Scales[0], noteEvents, changes, sections, metrics, fixedCandidate.Result.Track!,
            fixedCandidate.Scale, Metrics(fixedCandidate.Result.Report, 0, 0));
    }

    private static Transition EvaluateTransition(Phrase phrase, ShawzinScale source, ShawzinScale target,
        Candidate sourceCandidate, Candidate targetCandidate, DynamicShawzinScalePlanningSettings settings)
    {
        if (source == target) return Transition.Unchanged(phrase);
        var presses = WarframeShawzinScaleCycle.RequiredForwardPresses(source, target);
        var required = presses * (settings.ScaleKeyPressDurationSeconds + settings.ScaleKeyReleaseDelaySeconds)
            + settings.MinimumGapBeforeNextNoteSeconds;
        var timingSafe = phrase.PauseBefore >= required;
        var pitchErrorsPrevented = PitchErrorSum(sourceCandidate.Result.Report) - PitchErrorSum(targetCandidate.Result.Report);
        var substitutionsPrevented = sourceCandidate.Result.Report.PitchSubstitutionCount - targetCandidate.Result.Report.PitchSubstitutionCount;
        var benefit = targetCandidate.Score - sourceCandidate.Score;
        var stableSection = phrase.Notes.Count >= settings.MinimumNotesBeforeChange &&
            phrase.End.Seconds - phrase.Start.Seconds >= settings.MinimumSectionDurationSeconds;
        var worthwhile = benefit >= settings.ImprovementThreshold &&
            (pitchErrorsPrevented >= settings.MinimumPitchErrorsPrevented || substitutionsPrevented >= settings.MinimumSubstitutionsPrevented);
        var penalty = settings.ScaleChangeCost + presses * settings.ScaleKeyPressCost;
        return new(source, target, presses, required, timingSafe, stableSection && timingSafe && worthwhile,
            penalty, benefit, pitchErrorsPrevented, substitutionsPrevented);
    }

    private Candidate Arrange(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale,
        DynamicShawzinScalePlanningSettings settings)
    {
        var result = _arranger.Arrange(track, timeline, instrument, new ArrangementOptions
        {
            Scale = scale,
            Strategies = settings.ArrangementStrategies,
        });
        var score = result.Track is null ? -10_000m : result.Report.MusicalSimilarity.OverallScore
            + result.Report.ExactNoteCount * 20m / Math.Max(1, result.Report.SourceNoteCount)
            - result.Report.MeanPitchErrorSemitones * 2m
            - result.Report.DroppedNoteCount * 4m;
        return new(scale, result, score);
    }

    private static IReadOnlyList<Phrase> CreatePhrases(IReadOnlyList<MusicalEvent> notes, ProjectTimeline timeline, decimal phraseGap)
    {
        if (notes.Count == 0) return [];
        var groups = new List<List<MusicalEvent>> { new() { notes[0] } };
        var previousEnd = End(notes[0], timeline);
        foreach (var note in notes.Skip(1))
        {
            var start = timeline.ToAbsoluteTime(note.StartTime);
            if (start.Seconds - previousEnd.Seconds >= phraseGap) groups.Add([]);
            groups[^1].Add(note);
            var end = End(note, timeline);
            if (end.Seconds > previousEnd.Seconds) previousEnd = end;
        }
        var result = new List<Phrase>();
        var priorEnd = AbsoluteTime.Zero;
        foreach (var group in groups)
        {
            var start = timeline.ToAbsoluteTime(group.MinBy(value => value.StartTime.Ticks)!.StartTime);
            var end = group.Select(value => End(value, timeline)).MaxBy(value => value.Seconds);
            result.Add(new(group, start, end, priorEnd, Math.Max(0m, start.Seconds - priorEnd.Seconds), timeline));
            priorEnd = end;
        }
        return result;
    }

    private static DynamicShawzinQualityMetrics Aggregate(IReadOnlyList<ArrangementReport> reports, IReadOnlyList<ShawzinScaleChangeEvent> changes)
    {
        var source = reports.Sum(value => value.SourceNoteCount);
        var output = reports.Sum(value => value.OutputNoteCount);
        var meanError = source == 0 ? 0m : reports.Sum(value => value.MeanPitchErrorSemitones * value.SourceNoteCount) / source;
        var similarity = source == 0 ? 100m : reports.Sum(value => value.MusicalSimilarity.OverallScore * value.SourceNoteCount) / source;
        return new(source, output, reports.Sum(value => value.ExactNoteCount), reports.Sum(value => value.PitchSubstitutionCount),
            reports.Sum(value => value.OctaveShiftCount), reports.Sum(value => value.DroppedNoteCount), decimal.Round(meanError, 2),
            source == 0 ? 100m : decimal.Round(output * 100m / source, 1), decimal.Round(similarity, 1), changes.Count,
            changes.Sum(value => value.RequiredScaleKeyPressCount));
    }

    private static DynamicShawzinQualityMetrics Metrics(ArrangementReport report, int changes, int presses) =>
        new(report.SourceNoteCount, report.OutputNoteCount, report.ExactNoteCount, report.PitchSubstitutionCount,
            report.OctaveShiftCount, report.DroppedNoteCount, report.MeanPitchErrorSemitones,
            report.SourceNoteCount == 0 ? 100m : decimal.Round(report.OutputNoteCount * 100m / report.SourceNoteCount, 1),
            report.MusicalSimilarity.OverallScore, changes, presses);

    private static DynamicShawzinScalePlan EmptyPlan(MidiTrack source, Candidate fixedCandidate)
    {
        var track = fixedCandidate.Result.Track ?? new ShawzinTrack { Id = source.Id, Name = source.Name, Scale = fixedCandidate.Scale };
        var metrics = Metrics(fixedCandidate.Result.Report, 0, 0);
        return new(ShawzinArrangementMode.ShareCode, fixedCandidate.Scale, [], [], [], metrics, track, fixedCandidate.Scale, metrics);
    }

    private static MidiTrack Track(MidiTrack source, IReadOnlyList<MusicalEvent> notes) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Events = [.. notes],
    };

    private static AbsoluteTime End(MusicalEvent note, ProjectTimeline timeline) =>
        timeline.ToAbsoluteTime(new MusicalTime(checked(note.StartTime.Ticks + note.Duration.Ticks)));

    private static decimal QuantizedSeconds(MusicalEvent note, ProjectTimeline timeline) =>
        decimal.Round(timeline.ToAbsoluteTime(note.StartTime).Seconds / 0.0625m, 0, MidpointRounding.AwayFromZero) * 0.0625m;

    private static int PitchErrorSum(ArrangementReport report) => decimal.ToInt32(decimal.Round(
        report.MeanPitchErrorSemitones * report.SourceNoteCount, 0, MidpointRounding.AwayFromZero));

    private static bool IsBetter(PathState proposed, PathState existing) => proposed.Score > existing.Score ||
        proposed.Score == existing.Score && proposed.Transitions.Count(value => value.IsChange) < existing.Transitions.Count(value => value.IsChange);

    private static Guid StableId(Guid trackId, int section, ShawzinScale source, ShawzinScale target)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{trackId:N}|scale|{section}|{source}|{target}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static void Validate(DynamicShawzinScalePlanningSettings settings)
    {
        if (settings.PhraseGapSeconds < 0m || settings.MinimumSectionDurationSeconds < 0m || settings.ScaleChangeCost < 0m ||
            settings.ScaleKeyPressCost < 0m || settings.ImprovementThreshold < 0m || settings.ScaleKeyPressDurationSeconds < 0m ||
            settings.ScaleKeyReleaseDelaySeconds < 0m || settings.MinimumGapBeforeNextNoteSeconds < 0m)
            throw new ArgumentOutOfRangeException(nameof(settings), "Planning durations, costs and thresholds cannot be negative.");
        if (settings.MinimumNotesBeforeChange < 1 || settings.MinimumPitchErrorsPrevented < 0 || settings.MinimumSubstitutionsPrevented < 0)
            throw new ArgumentOutOfRangeException(nameof(settings));
    }

    private sealed record Phrase(IReadOnlyList<MusicalEvent> Notes, AbsoluteTime Start, AbsoluteTime End,
        AbsoluteTime PreviousEnd, decimal PauseBefore, ProjectTimeline Timeline);
    private sealed record Candidate(ShawzinScale Scale, ShawzinArrangementResult Result, decimal Score);
    private sealed record PathState(decimal Score, IReadOnlyList<ShawzinScale> Scales, IReadOnlyList<Transition> Transitions);
    private sealed record Transition(ShawzinScale Source, ShawzinScale Target, int Presses, decimal RequiredWindowSeconds,
        bool TimingSafe, bool Allowed, decimal Penalty, decimal Benefit, int PitchErrorsPrevented, int SubstitutionsPrevented)
    {
        public bool IsChange => Source != Target;
        public static Transition Unchanged(Phrase phrase) => new(default, default, 0, 0m, true, true, 0m, 0m, 0, 0);
    }
}
