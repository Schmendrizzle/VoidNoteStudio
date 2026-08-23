using System.Security.Cryptography;
using System.Text;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Arrangement;

/// <summary>Deterministically converts normalized music into reported Shawzin input events.</summary>
public sealed class ShawzinArranger(IShawzinPitchMapper mapper) : IShawzinArranger
{
    private const int MaximumTimestamp = 4095;
    private readonly IShawzinPitchMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    public ShawzinArrangementResult Arrange(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ArrangementOptions options)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var changes = new List<ArrangementChange>();
        var working = track.Events.OrderBy(value => value.StartTime.Ticks).ThenBy(value => value.Pitch).ThenBy(value => value.Id)
            .Select(note => Map(note, instrument, options, changes)).Where(value => value is not null).Cast<WorkingNote>().ToList();
        if (options.Strategies.HasFlag(ArrangementStrategy.Simplify)) Simplify(working, timeline, options, changes);

        var scheduled = new List<ScheduledStrike>();
        var collidedEvents = 0;
        var previousTimestamp = -1;
        foreach (var group in working.GroupBy(value => value.Source.StartTime.Ticks).OrderBy(value => value.Key))
        {
            var notes = group.ToList();
            var sourceSeconds = timeline.ToAbsoluteTime(notes[0].Source.StartTime).Seconds;
            var timestamp = Quantize(sourceSeconds, options.QuantizationStepSeconds);
            var chord = FindChord(notes);
            if (chord is not null)
            {
                if (!TrySchedule(notes, chord, timestamp, sourceSeconds, ref previousTimestamp, scheduled, timeline, options, changes, ref collidedEvents))
                    ReportUnresolved(notes, "The event collides after Shawzin timing quantization.", changes);
                continue;
            }

            if (notes.Count == 1)
            {
                ReportUnresolved(notes, "The pitch has no playable mapping under the selected strategies.", changes);
                continue;
            }

            if (options.Strategies.HasFlag(ArrangementStrategy.Arpeggiate) &&
                (notes.Count - 1) * options.QuantizationStepSeconds <= options.MaximumArpeggioSpreadSeconds)
            {
                for (var index = 0; index < notes.Count; index++)
                {
                    var note = notes[index];
                    var single = FindChord([note]);
                    if (single is null) { ReportUnresolved([note], "The note cannot be arpeggiated because it is unplayable.", changes); continue; }
                    var targetTimestamp = Math.Max(timestamp + index, previousTimestamp + 1);
                    if (targetTimestamp > MaximumTimestamp) { ReportUnresolved([note], "Arpeggiation exceeds the Shawzin timestamp range.", changes); continue; }
                    Schedule([note], single, targetTimestamp, sourceSeconds, scheduled, timeline, options, changes,
                        index == 0 && targetTimestamp == timestamp ? null : ArrangementStrategy.Arpeggiate);
                    previousTimestamp = targetTimestamp;
                }
                continue;
            }

            var retained = SelectVoices(notes, options.Strategies);
            if (retained is null)
            {
                ReportUnresolved(notes, "The simultaneous notes do not form a valid Shawzin chord.", changes);
                continue;
            }

            foreach (var dropped in notes.Except(retained))
                changes.Add(Change(dropped, null, null, ArrangementChangeType.DroppedNote, "The note was removed by the selected voice-reduction policy.", VoiceStrategy(options.Strategies)));
            var retainedChord = FindChord(retained);
            if (retainedChord is null || !TrySchedule(retained, retainedChord, timestamp, sourceSeconds, ref previousTimestamp, scheduled, timeline, options, changes, ref collidedEvents))
                ReportUnresolved(retained, "The retained melody notes collide after quantization.", changes);
        }

        var timingErrors = scheduled.Select(value => Math.Abs(value.SourceSeconds - value.Timestamp * options.QuantizationStepSeconds)).ToArray();
        var report = new ArrangementReport(
            changes,
            new ArrangementTimingMetrics(timingErrors.DefaultIfEmpty(0m).Max(), timingErrors.Length == 0 ? 0m : timingErrors.Average(), collidedEvents),
            track.Events.Count,
            scheduled.Count,
            scheduled.Sum(value => value.Notes.Count),
            CalculateSimilarity(track.Events, scheduled, timeline, options))
        {
            ExactNoteCount = scheduled.SelectMany(value => value.Notes).Count(value => value.Source.Pitch == value.TargetPitch),
            MeanPitchErrorSemitones = scheduled.SelectMany(value => value.Notes).Select(value => (decimal)Math.Abs(value.TargetPitch - value.Source.Pitch)).DefaultIfEmpty().Average(),
            MaximumPitchErrorSemitones = scheduled.SelectMany(value => value.Notes).Select(value => Math.Abs(value.TargetPitch - value.Source.Pitch)).DefaultIfEmpty().Max(),
        };
        if (report.HasUnresolvedConflicts) return new ShawzinArrangementResult(null, report);

        var shawzinEvents = scheduled.Select((value, index) => new ShawzinEvent(
            StableId(track.Id, value.Notes.Select(note => note.Source.Id), value.Timestamp, index),
            new AbsoluteTime(value.Timestamp * options.QuantizationStepSeconds),
            value.Chord)).ToList();
        var normalized = scheduled.SelectMany(value => value.Notes.Select(note => new MusicalEvent(
            StableId(track.Id, [note.Source.Id], value.Timestamp, note.TargetPitch),
            timeline.ToMusicalTime(new AbsoluteTime(value.Timestamp * options.QuantizationStepSeconds)),
            note.Source.Duration,
            note.TargetPitch,
            note.Source.Velocity,
            MusicalEventSource.Generated,
            note.Source.Confidence))).ToList();
        return new ShawzinArrangementResult(new ShawzinTrack
        {
            Name = $"{track.Name} – {instrument.DisplayName}",
            InstrumentId = instrument.Id,
            Scale = options.Scale,
            Events = normalized,
            ShawzinEvents = shawzinEvents,
        }, report);
    }

    private WorkingNote? Map(MusicalEvent source, ShawzinDefinition instrument, ArrangementOptions options, List<ArrangementChange> changes)
    {
        var pitch = source.Pitch;
        var strict = options.Strategies.HasFlag(ArrangementStrategy.Strict);
        if (options.AllowTransposition && options.TranspositionSemitones != 0)
        {
            if (strict)
            {
                changes.Add(new ArrangementChange(source.Id, source.Pitch, null, source.StartTime, null, ArrangementChangeType.ConflictUnresolved,
                    "Strict strategy forbids configured transposition.", ArrangementStrategy.Strict));
                return null;
            }
            var transposed = pitch + options.TranspositionSemitones;
            if (transposed is < 0 or > 127)
            {
                changes.Add(new ArrangementChange(source.Id, source.Pitch, null, source.StartTime, null, ArrangementChangeType.ConflictUnresolved,
                    "The configured transposition leaves the normalized MIDI pitch range.", ArrangementStrategy.Strict));
                return null;
            }
            pitch = transposed;
            changes.Add(new ArrangementChange(source.Id, source.Pitch, pitch, source.StartTime, source.StartTime, ArrangementChangeType.Transposed,
                $"The configured transposition changed the pitch by {options.TranspositionSemitones:+#;-#;0} semitones.", ArrangementStrategy.Strict));
        }

        var mapping = _mapper.Map(pitch, instrument, options.Scale);
        if (mapping.Kind == ShawzinPitchMappingKind.Exact) return new WorkingNote(source, pitch, mapping.Candidates);
        if (mapping.Kind == ShawzinPitchMappingKind.OctaveShiftable && options.Strategies.HasFlag(ArrangementStrategy.OctaveShift))
        {
            var delta = mapping.Candidates[0].SemitoneDelta;
            var target = mapping.Candidates[0].Pitch;
            changes.Add(new ArrangementChange(source.Id, source.Pitch, target, source.StartTime, source.StartTime, ArrangementChangeType.OctaveShift,
                $"Pitch was shifted {delta:+#;-#;0} semitones to a playable octave.", ArrangementStrategy.OctaveShift));
            return new WorkingNote(source, target, mapping.Candidates.Where(value => value.Pitch == target).ToArray());
        }
        if (options.Strategies.HasFlag(ArrangementStrategy.ClosestPitch))
        {
            var closest = _mapper.FindClosest(pitch, instrument, options.Scale);
            if (closest is not null)
            {
                changes.Add(new ArrangementChange(source.Id, source.Pitch, closest.Pitch, source.StartTime, source.StartTime, ArrangementChangeType.PitchSubstitution,
                    $"Pitch was moved {closest.SemitoneDelta:+#;-#;0} semitones to the nearest playable pitch.", ArrangementStrategy.ClosestPitch));
                var candidates = instrument.PlayProfile.Scales[options.Scale].Positions.Where(value => value.Pitch == closest.Pitch)
                    .Select(value => new ShawzinPitchCandidate(value.Pitch, value.Input, value.Pitch - pitch)).ToArray();
                return new WorkingNote(source, closest.Pitch, candidates);
            }
        }

        changes.Add(new ArrangementChange(source.Id, source.Pitch, null, source.StartTime, null, ArrangementChangeType.ConflictUnresolved,
            $"Pitch is {mapping.Kind} for the selected instrument and scale.", ArrangementStrategy.Strict));
        return null;
    }

    private static bool TrySchedule(IReadOnlyList<WorkingNote> notes, ShawzinChord chord, int timestamp, decimal sourceSeconds,
        ref int previousTimestamp, List<ScheduledStrike> scheduled, ProjectTimeline timeline, ArrangementOptions options,
        List<ArrangementChange> changes, ref int collidedEvents)
    {
        if (options.Strategies.HasFlag(ArrangementStrategy.Strict) && timestamp * options.QuantizationStepSeconds != sourceSeconds)
            return false;
        if (timestamp <= previousTimestamp)
        {
            collidedEvents++;
            if (!options.Strategies.HasFlag(ArrangementStrategy.Arpeggiate)) return false;
            timestamp = previousTimestamp + 1;
            if (timestamp > MaximumTimestamp || timestamp * options.QuantizationStepSeconds - sourceSeconds > options.MaximumArpeggioSpreadSeconds) return false;
            Schedule(notes, chord, timestamp, sourceSeconds, scheduled, timeline, options, changes, ArrangementStrategy.Arpeggiate);
        }
        else Schedule(notes, chord, timestamp, sourceSeconds, scheduled, timeline, options, changes, null);
        previousTimestamp = timestamp;
        return true;
    }

    private static void Schedule(IReadOnlyList<WorkingNote> notes, ShawzinChord chord, int timestamp, decimal sourceSeconds,
        List<ScheduledStrike> scheduled, ProjectTimeline timeline, ArrangementOptions options, List<ArrangementChange> changes, ArrangementStrategy? arpeggio)
    {
        scheduled.Add(new ScheduledStrike(timestamp, sourceSeconds, notes, chord));
        var newTime = timeline.ToMusicalTime(new AbsoluteTime(timestamp * options.QuantizationStepSeconds));
        foreach (var note in notes)
        {
            if (arpeggio is not null)
                changes.Add(Change(note, note.TargetPitch, newTime, ArrangementChangeType.Arpeggiated, "The note was moved to resolve a simultaneous input or timing collision.", arpeggio.Value));
            else if (newTime != note.Source.StartTime)
                changes.Add(Change(note, note.TargetPitch, newTime, ArrangementChangeType.Quantized, "The event was rounded once at the 1/16-second Shawzin format boundary.", ArrangementStrategy.Strict));
        }
    }

    private static ShawzinChord? FindChord(IReadOnlyList<WorkingNote> notes)
    {
        if (notes.Count is < 1 or > 3) return null;
        var chosen = new ShawzinNote[notes.Count];
        return Search(0, null, new HashSet<ShawzinString>()) ? new ShawzinChord(chosen) : null;

        bool Search(int index, ShawzinFret? frets, HashSet<ShawzinString> strings)
        {
            if (index == notes.Count) return true;
            foreach (var candidate in notes[index].Candidates)
            {
                if (frets is not null && candidate.Input.Frets != frets || !strings.Add(candidate.Input.String)) continue;
                chosen[index] = candidate.Input;
                if (Search(index + 1, frets ?? candidate.Input.Frets, strings)) return true;
                strings.Remove(candidate.Input.String);
            }
            return false;
        }
    }

    private static IReadOnlyList<WorkingNote>? SelectVoices(IReadOnlyList<WorkingNote> notes, ArrangementStrategy strategies)
    {
        WorkingNote[] ordered;
        if (strategies.HasFlag(ArrangementStrategy.PreserveMelody) || strategies.HasFlag(ArrangementStrategy.DropLowest))
            ordered = notes.OrderByDescending(value => value.TargetPitch).ThenByDescending(value => value.Source.Velocity).ThenBy(value => value.Source.Id).ToArray();
        else if (strategies.HasFlag(ArrangementStrategy.DropHighest))
            ordered = notes.OrderBy(value => value.TargetPitch).ThenByDescending(value => value.Source.Velocity).ThenBy(value => value.Source.Id).ToArray();
        else return null;

        for (var size = Math.Min(3, ordered.Length); size >= 1; size--)
        {
            foreach (var candidate in Combinations(ordered, size))
            {
                if (FindChord(candidate) is not null) return candidate;
            }
        }
        return null;
    }

    private static IEnumerable<IReadOnlyList<WorkingNote>> Combinations(IReadOnlyList<WorkingNote> notes, int size)
    {
        var indexes = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return indexes.Select(index => notes[index]).ToArray();
            var cursor = size - 1;
            while (cursor >= 0 && indexes[cursor] == notes.Count - size + cursor) cursor--;
            if (cursor < 0) yield break;
            indexes[cursor]++;
            for (var index = cursor + 1; index < size; index++) indexes[index] = indexes[index - 1] + 1;
        }
    }

    private static void Simplify(List<WorkingNote> notes, ProjectTimeline timeline, ArrangementOptions options, List<ArrangementChange> changes)
    {
        var ordered = notes.OrderBy(value => value.Source.StartTime.Ticks).ThenByDescending(value => value.Source.Velocity).ToArray();
        var retained = new Queue<decimal>();
        foreach (var note in ordered)
        {
            var seconds = timeline.ToAbsoluteTime(note.Source.StartTime).Seconds;
            while (retained.Count > 0 && seconds - retained.Peek() >= 1m) retained.Dequeue();
            if (retained.Count >= options.MaximumNotesPerSecond)
            {
                notes.Remove(note);
                changes.Add(Change(note, null, null, ArrangementChangeType.DroppedNote, "The note exceeded the configured one-second density limit.", ArrangementStrategy.Simplify));
            }
            else retained.Enqueue(seconds);
        }
    }

    private static void ReportUnresolved(IEnumerable<WorkingNote> notes, string reason, List<ArrangementChange> changes)
    {
        foreach (var note in notes)
            changes.Add(Change(note, null, null, ArrangementChangeType.ConflictUnresolved, reason, ArrangementStrategy.Strict));
    }

    private static ArrangementChange Change(WorkingNote note, int? pitch, MusicalTime? newTime, ArrangementChangeType type, string reason, ArrangementStrategy strategy) =>
        new(note.Source.Id, note.Source.Pitch, pitch, note.Source.StartTime, newTime, type, reason, strategy);

    private static ArrangementStrategy VoiceStrategy(ArrangementStrategy strategies) =>
        strategies.HasFlag(ArrangementStrategy.DropHighest) ? ArrangementStrategy.DropHighest :
        strategies.HasFlag(ArrangementStrategy.DropLowest) ? ArrangementStrategy.DropLowest : ArrangementStrategy.PreserveMelody;

    private static int Quantize(decimal seconds, decimal step) => checked((int)decimal.Round(seconds / step, 0, MidpointRounding.AwayFromZero));

    private static void Validate(ArrangementOptions options)
    {
        if (options.QuantizationStepSeconds != 0.0625m) throw new ArgumentOutOfRangeException(nameof(options), "Recorded Song V1 requires a 0.0625-second grid.");
        if (options.MaximumArpeggioSpreadSeconds < 0m) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumNotesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.TranspositionSemitones is < -12 or > 12) throw new ArgumentOutOfRangeException(nameof(options));
    }

    private static MusicalSimilarityReport CalculateSimilarity(
        IReadOnlyList<MusicalEvent> source,
        IReadOnlyList<ScheduledStrike> scheduled,
        ProjectTimeline timeline,
        ArrangementOptions options)
    {
        if (source.Count == 0) return new(100m, 100m, 100m, 100m, 100m, 100m);
        var arranged = scheduled.SelectMany(strike => strike.Notes.Select(note => new SimilarityNote(
            note.Source,
            note.TargetPitch,
            strike.Timestamp * options.QuantizationStepSeconds))).ToDictionary(value => value.Source.Id);
        var retained = source.Where(value => arranged.ContainsKey(value.Id))
            .OrderBy(value => value.StartTime.Ticks).ThenBy(value => value.Pitch).ThenBy(value => value.Id).ToArray();
        var pitch = source.Average(note => arranged.TryGetValue(note.Id, out var target)
            ? PitchPreservation(note.Pitch, target.Pitch)
            : 0m);
        var retention = retained.Length * 100m / source.Count;
        var timing = source.Average(note => arranged.TryGetValue(note.Id, out var target)
            ? 100m * Math.Max(0m, 1m - Math.Abs(timeline.ToAbsoluteTime(note.StartTime).Seconds - target.Seconds) / 0.25m)
            : 0m);
        var contour = PairScore(retained, arranged, static (leftSource, rightSource, leftTarget, rightTarget) =>
            Math.Sign(rightSource.Pitch - leftSource.Pitch) == Math.Sign(rightTarget.Pitch - leftTarget.Pitch) ? 100m : 0m);
        var intervals = PairScore(retained, arranged, static (leftSource, rightSource, leftTarget, rightTarget) =>
            100m * Math.Max(0m, 1m - Math.Abs((rightTarget.Pitch - leftTarget.Pitch) - (rightSource.Pitch - leftSource.Pitch)) / 12m));
        var overall = 0.35m * pitch + 0.20m * contour + 0.20m * retention + 0.15m * timing + 0.10m * intervals;
        return new(decimal.Round(overall, 1), decimal.Round(pitch, 1), decimal.Round(contour, 1), decimal.Round(retention, 1),
            decimal.Round(timing, 1), decimal.Round(intervals, 1));
    }

    private static decimal PitchPreservation(int source, int target)
    {
        var distance = Math.Abs(target - source);
        if (distance == 0) return 100m;
        if (distance % 12 == 0) return Math.Max(40m, 70m - (distance / 12 - 1) * 15m);
        return 100m * Math.Max(0m, 1m - distance / 12m);
    }

    private static decimal PairScore(
        IReadOnlyList<MusicalEvent> retained,
        IReadOnlyDictionary<Guid, SimilarityNote> arranged,
        Func<MusicalEvent, MusicalEvent, SimilarityNote, SimilarityNote, decimal> score)
    {
        if (retained.Count < 2) return retained.Count == 0 ? 0m : 100m;
        return retained.Zip(retained.Skip(1), (left, right) => score(left, right, arranged[left.Id], arranged[right.Id])).Average();
    }

    private static Guid StableId(Guid trackId, IEnumerable<Guid> sourceIds, int timestamp, int discriminator)
    {
        var text = $"{trackId:N}|{string.Join(',', sourceIds.Order())}|{timestamp}|{discriminator}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record WorkingNote(MusicalEvent Source, int TargetPitch, IReadOnlyList<ShawzinPitchCandidate> Candidates);
    private sealed record ScheduledStrike(int Timestamp, decimal SourceSeconds, IReadOnlyList<WorkingNote> Notes, ShawzinChord Chord);
    private sealed record SimilarityNote(MusicalEvent Source, int Pitch, decimal Seconds);
}
