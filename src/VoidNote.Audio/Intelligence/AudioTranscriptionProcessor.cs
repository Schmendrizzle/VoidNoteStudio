using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Intelligence;

public static class AudioTranscriptionProcessor
{
    public static TranscriptionWorkflowResult Create(ProjectTimeline timeline, AudioProcessingSource source, Guid? stemId, string trackName,
        TranscriptionEngineResult result, AudioTranscriptionSettings settings, TimeSpan processingDuration)
    {
        Validate(settings);
        var changes = new List<TranscriptionChange>();
        var drafts = result.Notes.Select(note => Draft.Create(note, timeline, source.StartOffset, Confidence(note.Confidence, settings))).ToList();
        if (settings.Mode == TranscriptionMode.Monophonic) ReduceToMonophonic(drafts, changes);
        if (settings.RemoveGhostNotes)
            drafts.RemoveAll(note => Remove(note, note.DurationSeconds < settings.MinimumNoteDuration.Seconds, TranscriptionChangeType.GhostNoteRemoved, "Note was shorter than the configured minimum.", changes));
        if (settings.DetectDuplicates) RemoveDuplicates(drafts, changes);
        if (settings.MergeAdjacentNotes) drafts = MergeAdjacent(drafts, settings.MergeGap, changes);
        if (settings.MarkPitchOutliers) MarkOutliers(drafts, changes);
        drafts.RemoveAll(note => Remove(note, ShouldFilter(note.Confidence, note.Level, settings), TranscriptionChangeType.ConfidenceFiltered,
            $"Confidence {note.Confidence:F3} did not satisfy the configured filter.", changes));

        var track = new MidiTrack { Name = $"{trackName} transcription" };
        foreach (var draft in drafts.OrderBy(value => value.RawStart.Ticks).ThenBy(value => value.Pitch))
        {
            var start = Quantize(draft.RawStart, timeline.TicksPerQuarterNote, settings.Quantization);
            var end = Quantize(new MusicalTime(draft.RawStart.Ticks + draft.RawDuration.Ticks), timeline.TicksPerQuarterNote, settings.Quantization);
            var duration = new MusicalTime(Math.Max(1, end.Ticks - start.Ticks));
            var id = Guid.NewGuid();
            if (start != draft.RawStart || duration != draft.RawDuration) changes.Add(new(id, TranscriptionChangeType.Quantized, "Timing was projected to the selected grid; raw timing remains in provenance."));
            track.Events.Add(new(id, start, duration, draft.Pitch, draft.Velocity, MusicalEventSource.AudioTranscription, draft.Confidence,
                new() { SourceAudioId = source.AudioSourceId, SourceStemId = stemId, Engine = result.Engine, EngineVersion = result.EngineVersion,
                    RawConfidence = draft.Confidence, ConfidenceLevel = draft.Level, OriginalStart = draft.RawStart, OriginalDuration = draft.RawDuration }));
        }
        var all = result.Notes;
        var levels = all.Select(value => Confidence(value.Confidence, settings)).ToArray();
        var report = new AudioTranscriptionReport
        {
            Name = $"{track.Name} report", MidiTrackId = track.Id, Source = source, DetectedNotes = all.Count, KeptNotes = track.Events.Count,
            DiscardedNotes = all.Count - track.Events.Count, AverageConfidence = all.Count == 0 ? 0 : all.Average(value => value.Confidence),
            HighConfidenceCount = levels.Count(value => value == NoteConfidenceLevel.High), MediumConfidenceCount = levels.Count(value => value == NoteConfidenceLevel.Medium),
            LowConfidenceCount = levels.Count(value => value == NoteConfidenceLevel.Low), AnalyzedDuration = source.Duration,
            MinimumPitch = all.Count == 0 ? null : all.Min(value => value.Pitch), MaximumPitch = all.Count == 0 ? null : all.Max(value => value.Pitch),
            NoteDensityPerSecond = source.Duration.Seconds <= 0 ? 0 : all.Count / source.Duration.Seconds,
            Engine = result.Engine, EngineVersion = result.EngineVersion, ProcessingDuration = processingDuration, Settings = settings, Changes = changes,
        };
        return new(track, report);
    }

    public static NoteConfidenceLevel Confidence(decimal raw, AudioTranscriptionSettings settings) => raw >= settings.HighConfidenceThreshold
        ? NoteConfidenceLevel.High : raw >= settings.MediumConfidenceThreshold ? NoteConfidenceLevel.Medium : NoteConfidenceLevel.Low;

    private static bool ShouldFilter(decimal confidence, NoteConfidenceLevel level, AudioTranscriptionSettings settings) => settings.ConfidenceFilter switch
    {
        ConfidenceFilterMode.RemoveLow => level == NoteConfidenceLevel.Low,
        ConfidenceFilterMode.MinimumThreshold => confidence < settings.MinimumConfidence,
        _ => false,
    };

    private static bool Remove(Draft note, bool remove, TranscriptionChangeType type, string reason, List<TranscriptionChange> changes)
    { if (remove) changes.Add(new(null, type, $"Pitch {note.Pitch}: {reason}")); return remove; }

    private static void RemoveDuplicates(List<Draft> notes, List<TranscriptionChange> changes)
    {
        foreach (var group in notes.GroupBy(value => (value.Pitch, value.RawStart.Ticks)).Where(value => value.Count() > 1).ToArray())
        {
            foreach (var duplicate in group.OrderByDescending(value => value.Confidence).Skip(1).ToArray())
            { notes.Remove(duplicate); changes.Add(new(null, TranscriptionChangeType.DuplicateRemoved, $"Removed duplicate pitch {duplicate.Pitch} at tick {duplicate.RawStart.Ticks}.")); }
        }
    }

    private static void ReduceToMonophonic(List<Draft> notes, List<TranscriptionChange> changes)
    {
        Draft? active = null;
        foreach (var note in notes.OrderBy(value => value.StartSeconds).ThenByDescending(value => value.Confidence).ToArray())
        {
            if (active is null || note.StartSeconds >= active.EndSeconds) { active = note; continue; }
            var remove = note.Confidence > active.Confidence ? active : note;
            var keep = ReferenceEquals(remove, note) ? active : note;
            notes.Remove(remove); active = keep;
            changes.Add(new(null, TranscriptionChangeType.PolyphonyReduced, $"Monophonic mode removed overlapping pitch {remove.Pitch}; pitch {keep.Pitch} had higher confidence."));
        }
    }

    private static List<Draft> MergeAdjacent(List<Draft> notes, AbsoluteTime gap, List<TranscriptionChange> changes)
    {
        var output = new List<Draft>();
        foreach (var note in notes.OrderBy(value => value.Pitch).ThenBy(value => value.RawStart.Ticks))
        {
            var previous = output.LastOrDefault(value => value.Pitch == note.Pitch);
            if (previous is not null && note.StartSeconds - previous.EndSeconds <= gap.Seconds && note.StartSeconds >= previous.StartSeconds)
            {
                output.Remove(previous); output.Add(previous.Merge(note));
                changes.Add(new(null, TranscriptionChangeType.NotesMerged, $"Merged adjacent pitch {note.Pitch} detections."));
            }
            else output.Add(note);
        }
        return output;
    }

    private static void MarkOutliers(List<Draft> notes, List<TranscriptionChange> changes)
    {
        if (notes.Count < 4) return;
        var ordered = notes.Select(value => value.Pitch).Order().ToArray(); var median = ordered[ordered.Length / 2];
        foreach (var note in notes.Where(value => Math.Abs(value.Pitch - median) > 24))
            changes.Add(new(null, TranscriptionChangeType.PitchOutlierMarked, $"Pitch {note.Pitch} is more than two octaves from median {median}; it was retained."));
    }

    private static MusicalTime Quantize(MusicalTime value, int ppq, TranscriptionQuantization quantization)
    {
        if (quantization == TranscriptionQuantization.None) return value;
        var grid = quantization switch
        {
            TranscriptionQuantization.Quarter => ppq,
            TranscriptionQuantization.Eighth => ppq / 2m,
            TranscriptionQuantization.Sixteenth => ppq / 4m,
            TranscriptionQuantization.ThirtySecond => ppq / 8m,
            TranscriptionQuantization.EighthTriplet => ppq / 3m,
            TranscriptionQuantization.SixteenthTriplet => ppq / 6m,
            _ => ppq,
        };
        return new((long)(Math.Round(value.Ticks / grid, MidpointRounding.AwayFromZero) * grid));
    }

    private static void Validate(AudioTranscriptionSettings settings)
    {
        if (settings.HighConfidenceThreshold is < 0 or > 1 || settings.MediumConfidenceThreshold is < 0 or > 1 || settings.MinimumConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings));
        if (settings.HighConfidenceThreshold < settings.MediumConfidenceThreshold)
            throw new ArgumentException("High confidence threshold cannot be below medium confidence threshold.", nameof(settings));
    }

    private sealed record Draft(int Pitch, int Velocity, decimal Confidence, NoteConfidenceLevel Level, MusicalTime RawStart, MusicalTime RawDuration, decimal StartSeconds, decimal DurationSeconds)
    {
        public decimal EndSeconds => StartSeconds + DurationSeconds;
        public static Draft Create(DetectedAudioNote note, ProjectTimeline timeline, AbsoluteTime timelineOffset, NoteConfidenceLevel level)
        {
            var startSeconds = timelineOffset.Seconds + note.Start.Seconds;
            var endSeconds = startSeconds + note.Duration.Seconds;
            var start = timeline.ToMusicalTime(new(startSeconds)); var end = timeline.ToMusicalTime(new(endSeconds));
            return new(note.Pitch, Math.Clamp((int)Math.Round(note.Velocity * 127m, MidpointRounding.AwayFromZero), 1, 127), note.Confidence, level,
                start, new(Math.Max(1, end.Ticks - start.Ticks)), startSeconds, note.Duration.Seconds);
        }
        public Draft Merge(Draft next)
        {
            var end = Math.Max(RawStart.Ticks + RawDuration.Ticks, next.RawStart.Ticks + next.RawDuration.Ticks);
            return this with { RawDuration = new(end - RawStart.Ticks), DurationSeconds = Math.Max(EndSeconds, next.EndSeconds) - StartSeconds,
                Confidence = Math.Max(Confidence, next.Confidence), Velocity = Math.Max(Velocity, next.Velocity), Level = Confidence >= next.Confidence ? Level : next.Level };
        }
    }
}
