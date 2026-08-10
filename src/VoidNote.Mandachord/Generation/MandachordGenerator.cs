using System.Security.Cryptography;
using System.Text;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Mandachord.Mapping;

namespace VoidNote.Mandachord.Generation;

public sealed class MandachordGenerator(IMandachordPitchMapper pitchMapper, IMandachordTimingMapper timingMapper) : IMandachordGenerator
{
    public const string Version = "1.0.0";
    private static readonly MandachordGenerationPreset[] Alternatives = [MandachordGenerationPreset.Faithful, MandachordGenerationPreset.Recognizable, MandachordGenerationPreset.Gameplay, MandachordGenerationPreset.RhythmFocus, MandachordGenerationPreset.MelodyFocus];

    public MandachordGenerationResult Generate(ProjectTimeline timeline, IReadOnlyList<MandachordSourceTrack> sources, MandachordGenerationPreset preset, MandachordGenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(timeline); ArgumentNullException.ThrowIfNull(sources); ArgumentNullException.ThrowIfNull(settings);
        if (sources.Count == 0) throw new ArgumentException("At least one normalized or analyzed source is required.", nameof(sources));
        if (settings.CandidateCount is < 1 or > 5 || settings.MaximumLayerDensity is <= 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(settings));
        foreach (var source in sources) source.Validate();
        var variants = new[] { preset }.Concat(Alternatives.Where(value => value != preset)).Take(settings.CandidateCount).ToArray();
        var candidates = variants.Select((variant, index) => GenerateCandidate(timeline, sources, variant, settings, index)).OrderByDescending(value => RankingScore(value.Report.Scores, preset)).ThenBy(value => value.Arrangement.Id).ToArray();
        return new(candidates.Select((value, index) => value with { Rank = index + 1 }).ToArray());
    }

    private MandachordGenerationCandidate GenerateCandidate(ProjectTimeline timeline, IReadOnlyList<MandachordSourceTrack> sources, MandachordGenerationPreset preset, MandachordGenerationSettings settings, int variantIndex)
    {
        var report = new MandachordGenerationReport { GeneratorVersion = Version, Preset = preset };
        var patternId = StableId("pattern", sources, preset, settings, variantIndex);
        var pattern = new MandachordPattern { Id = patternId, Name = $"{settings.SectionName} - {preset}", Section = settings.SectionName, Preset = preset, GenerationSource = string.Join(", ", sources.Select(value => value.Name)), CreatedAt = DateTimeOffset.UnixEpoch, ModifiedAt = DateTimeOffset.UnixEpoch };
        var tonal = sources.SelectMany(source => source.Events.Select(note => (source, note))).OrderBy(value => value.note.StartTime.Ticks).ThenBy(value => value.note.Pitch).ThenBy(value => value.note.Id).ToArray();
        report.SourceNoteCount = tonal.Length;
        AddTonalLayer(pattern, report, timeline, tonal.Where(value => value.source.PreferredLayer == MandachordLayer.Bass || value.source.PreferredLayer is null && value.note.Pitch < 55).ToArray(), MandachordLayer.Bass, preset, settings);
        AddTonalLayer(pattern, report, timeline, tonal.Where(value => value.source.PreferredLayer == MandachordLayer.Melody || value.source.PreferredLayer is null && value.note.Pitch >= 55).ToArray(), MandachordLayer.Melody, preset, settings);
        AddPercussion(pattern, report, timeline, sources, preset, settings);
        pattern.Steps.Sort(StepComparer.Instance);
        report.Scores = MandachordScoring.Calculate(report.SourceNoteCount, pattern.Steps, report, preset);
        var arrangementId = StableId("arrangement", sources, preset, settings, variantIndex);
        var arrangement = new MandachordArrangement { Id = arrangementId, Name = $"{preset} candidate", Preset = preset, SelectedSoundSetId = settings.SoundSetId ?? BuiltInMandachordSoundSets.SyntheticDefault().Id, Patterns = [pattern], Sections = [new MandachordSection { Id = StableId("section", sources, preset, settings, variantIndex), Name = settings.SectionName, Start = settings.LoopStart, End = timeline.FromBeats(timeline.ToBeats(settings.LoopStart) + MandachordGridDefinition.Standard.LoopBeats), PatternId = pattern.Id }] };
        arrangement.Validate();
        return new(arrangement, report, 0);
    }

    private void AddTonalLayer(MandachordPattern pattern, MandachordGenerationReport report, ProjectTimeline timeline,
        IReadOnlyList<(MandachordSourceTrack source, MusicalEvent note)> notes, MandachordLayer layer, MandachordGenerationPreset preset, MandachordGenerationSettings settings)
    {
        if (notes.Count == 0) return;
        var timing = timingMapper.Map(timeline, notes.Select(value => value.note), settings.LoopStart).ToDictionary(value => value.SourceEventId);
        var repetition = notes.GroupBy(value => value.note.Pitch).ToDictionary(value => value.Key, value => value.Count());
        var limit = LayerLimit(layer, preset, settings);
        var ranked = notes.Select((value, index) =>
        {
            var previous = index > 0 ? notes[index - 1].note : null;
            var continuity = previous is null ? 1m : 1m - Math.Min(1m, Math.Abs(value.note.Pitch - previous.Pitch) / 24m);
            var duration = Math.Min(1m, timeline.ToBeats(value.note.Duration) / 2m);
            var velocity = value.note.Velocity / 127m;
            var repeat = Math.Min(1m, repetition[value.note.Pitch] / 4m);
            var register = layer == MandachordLayer.Bass ? 1m - value.note.Pitch / 127m : value.note.Pitch / 127m;
            var importance = layer == MandachordLayer.Bass
                ? 0.28m * register + 0.24m * duration + 0.20m * continuity + 0.18m * repeat + 0.10m * velocity
                : 0.20m * register + 0.18m * duration + 0.22m * continuity + 0.20m * repeat + 0.20m * velocity;
            if (preset == MandachordGenerationPreset.MelodyFocus && layer == MandachordLayer.Melody) importance += 0.15m;
            if (preset == MandachordGenerationPreset.Recognizable && repeat > 0.5m) importance += 0.12m;
            return (value.source, value.note, mapping: timing[value.note.Id], importance);
        }).OrderByDescending(value => value.importance).ThenBy(value => value.note.StartTime.Ticks).ThenBy(value => value.note.Pitch).ThenBy(value => value.note.Id).ToArray();

        var occupied = new HashSet<int>();
        foreach (var item in ranked)
        {
            var mappedPitch = pitchMapper.Map(item.note.Pitch, layer, settings.Transposition);
            if (mappedPitch.Pitch is null || occupied.Contains(item.mapping.StepIndex) || occupied.Count >= limit)
            {
                report.DroppedNotes++; if (occupied.Contains(item.mapping.StepIndex)) report.Collisions++;
                report.Changes.Add(new(item.note.Id, null, occupied.Contains(item.mapping.StepIndex) ? MandachordChangeType.CollisionResolved : MandachordChangeType.Dropped,
                    mappedPitch.Pitch is null ? mappedPitch.Reason : "Lower-ranked note removed from a one-note-per-layer step collision.", item.note.Pitch, null, item.note.StartTime, item.mapping.StepIndex));
                continue;
            }
            occupied.Add(item.mapping.StepIndex);
            var id = StableId($"{pattern.Id:N}:{layer}:{item.mapping.StepIndex}:{mappedPitch.Pitch.Position}");
            var step = new MandachordStep { Id = id, Name = $"{layer} {item.mapping.StepIndex + 1}", Layer = layer, StepIndex = item.mapping.StepIndex, PitchPosition = mappedPitch.Pitch.Position, Velocity = item.note.Velocity,
                Provenance = new() { SourceTrackId = item.source.Id, SourceEventId = item.note.Id, GeneratorVersion = Version, Preset = preset, EditKind = MandachordStepEditKind.Generated } };
            pattern.Steps.Add(step);
            var pitchChanged = mappedPitch.SemitoneChange != 0; var timingChanged = item.mapping.TimingErrorSteps != 0;
            if (pitchChanged) { report.PitchChanges++; report.ShiftedNotes++; }
            else report.PreservedNotes++;
            if (timingChanged) report.TimingChanges++;
            report.Changes.Add(new(item.note.Id, step.Id, pitchChanged ? MandachordChangeType.PitchChanged : timingChanged ? MandachordChangeType.TimingChanged : MandachordChangeType.Preserved,
                $"{mappedPitch.Reason} Quantization error {item.mapping.TimingErrorSteps:0.###} step(s).", item.note.Pitch, mappedPitch.Pitch.PreviewMidiPitch, item.note.StartTime, item.mapping.StepIndex));
        }
    }

    private static void AddPercussion(MandachordPattern pattern, MandachordGenerationReport report, ProjectTimeline timeline, IReadOnlyList<MandachordSourceTrack> sources, MandachordGenerationPreset preset, MandachordGenerationSettings settings)
    {
        var explicitRhythm = sources.SelectMany(source => (source.RhythmEvents ?? []).Select(value => (source, rhythm: value))).ToArray();
        var events = explicitRhythm.Length > 0 ? explicitRhythm : sources.SelectMany(source => source.Events.Select(note =>
        {
            var beat = timeline.ToBeats(note.StartTime);
            var step = decimal.ToInt32(decimal.Round(beat * 4m, 0, MidpointRounding.AwayFromZero));
            var category = step % 16 is 0 or 8 ? MandachordPercussionCategory.Kick : step % 16 is 4 or 12 ? MandachordPercussionCategory.Snare : MandachordPercussionCategory.HiHat;
            return (source, rhythm: new MandachordRhythmEvent(note.StartTime, category, note.Velocity / 127m, note.Id));
        })).ToArray();
        var density = preset switch { MandachordGenerationPreset.RhythmFocus => 32, MandachordGenerationPreset.Gameplay => 24, MandachordGenerationPreset.MelodyFocus => 12, _ => 20 };
        var selected = events.Select(value =>
        {
            var exact = (timeline.ToBeats(value.rhythm.Start) - timeline.ToBeats(settings.LoopStart)) * 4m;
            var raw = decimal.ToInt64(decimal.Round(exact, 0, MidpointRounding.AwayFromZero));
            var step = (int)((raw % 64 + 64) % 64);
            return (value.source, value.rhythm, step, error: raw - exact);
        }).GroupBy(value => (value.step, value.rhythm.Category)).Select(group => group.OrderByDescending(value => value.rhythm.Strength).ThenBy(value => value.rhythm.SourceEventId).First())
          .OrderByDescending(value => value.rhythm.Strength).ThenBy(value => value.step).ThenBy(value => value.rhythm.Category).Take(density).OrderBy(value => value.step).ThenBy(value => value.rhythm.Category).ToArray();
        foreach (var item in selected)
        {
            var id = StableId($"{pattern.Id:N}:Percussion:{item.step}:{item.rhythm.Category}");
            pattern.Steps.Add(new() { Id = id, Name = $"{item.rhythm.Category} {item.step + 1}", Layer = MandachordLayer.Percussion, StepIndex = item.step, PercussionCategory = item.rhythm.Category, Velocity = Math.Clamp(decimal.ToInt32(decimal.Round(item.rhythm.Strength * 127m, 0, MidpointRounding.AwayFromZero)), 1, 127), Provenance = new() { SourceTrackId = item.source.Id, SourceEventId = item.rhythm.SourceEventId, GeneratorVersion = Version, Preset = preset, EditKind = MandachordStepEditKind.Generated } });
            report.Changes.Add(new(item.rhythm.SourceEventId, id, MandachordChangeType.LayerGenerated, explicitRhythm.Length > 0 ? "Mapped existing rhythm-analysis metadata." : "Derived rhythm only from normalized note onsets; no pitched drum notes were invented.", null, null, item.rhythm.Start, item.step));
        }
    }

    private static int LayerLimit(MandachordLayer layer, MandachordGenerationPreset preset, MandachordGenerationSettings settings)
    {
        var baseLimit = decimal.ToInt32(decimal.Floor(64m * settings.MaximumLayerDensity));
        var factor = (layer, preset) switch { (MandachordLayer.Melody, MandachordGenerationPreset.MelodyFocus) => 1m, (MandachordLayer.Melody, MandachordGenerationPreset.Recognizable) => 0.9m, (MandachordLayer.Bass, MandachordGenerationPreset.Gameplay) => 0.65m, (_, MandachordGenerationPreset.RhythmFocus) => 0.5m, _ => 0.75m };
        return Math.Clamp(decimal.ToInt32(decimal.Floor(baseLimit * factor)), 1, 64);
    }

    private static decimal RankingScore(MandachordScores scores, MandachordGenerationPreset requested) => requested switch
    {
        MandachordGenerationPreset.Gameplay => scores.Gameplay,
        MandachordGenerationPreset.RhythmFocus => scores.RhythmMatch,
        MandachordGenerationPreset.MelodyFocus => scores.MelodyPreservation,
        MandachordGenerationPreset.Recognizable => 0.6m * scores.MelodyPreservation + 0.4m * scores.Similarity,
        _ => scores.Similarity,
    };

    private static Guid StableId(string kind, IEnumerable<MandachordSourceTrack> sources, MandachordGenerationPreset preset, MandachordGenerationSettings settings, int variant)
    {
        var sourceSignature = string.Join('|', sources.OrderBy(value => value.Id).Select(value => $"{value.Id:N}:{string.Join(',', value.Events.OrderBy(note => note.StartTime.Ticks).ThenBy(note => note.Pitch).ThenBy(note => note.Id).Select(note => $"{note.Id:N}/{note.StartTime.Ticks}/{note.Duration.Ticks}/{note.Pitch}/{note.Velocity}"))}"));
        return StableId($"{kind}|{sourceSignature}|{preset}|{settings.LoopStart.Ticks}|{settings.Transposition}|{settings.SectionName}|{variant}");
    }
    internal static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private sealed class StepComparer : IComparer<MandachordStep>
    {
        public static StepComparer Instance { get; } = new();
        public int Compare(MandachordStep? x, MandachordStep? y) => x is null ? -1 : y is null ? 1 : (x.StepIndex, x.Layer, x.PitchPosition ?? -1, x.PercussionCategory ?? 0, x.Id).CompareTo((y.StepIndex, y.Layer, y.PitchPosition ?? -1, y.PercussionCategory ?? 0, y.Id));
    }
}

public static class MandachordScoring
{
    public static MandachordScores Calculate(int sourceNotes, IReadOnlyList<MandachordStep> steps, MandachordGenerationReport report, MandachordGenerationPreset preset)
    {
        var tonal = steps.Count(value => value.Layer != MandachordLayer.Percussion); var percussion = steps.Count - tonal;
        var represented = sourceNotes == 0 ? 1m : Math.Clamp(tonal / (decimal)sourceNotes, 0m, 1m);
        var pitchAccuracy = tonal == 0 ? (sourceNotes == 0 ? 1m : 0m) : 1m - report.PitchChanges / (decimal)Math.Max(1, tonal);
        var timingAccuracy = tonal == 0 ? (sourceNotes == 0 ? 1m : 0m) : 1m - report.TimingChanges / (decimal)Math.Max(1, tonal);
        var similarity = Percent(0.45m * represented + 0.35m * pitchAccuracy + 0.20m * timingAccuracy);
        var melodySteps = steps.Count(value => value.Layer == MandachordLayer.Melody); var bassSteps = steps.Count(value => value.Layer == MandachordLayer.Bass);
        var melody = Percent(Math.Clamp((melodySteps / (decimal)Math.Max(1, tonal)) * 1.4m, 0m, 1m) * 0.55m + pitchAccuracy * 0.45m);
        var bass = Percent(Math.Clamp((bassSteps / (decimal)Math.Max(1, tonal)) * 1.8m, 0m, 1m) * 0.60m + pitchAccuracy * 0.40m);
        var rhythm = Percent(Math.Clamp(percussion / 24m, 0m, 1m) * 0.65m + timingAccuracy * 0.35m);
        var densityRatio = steps.Count / (64m * 3m); var density = Percent(1m - Math.Min(1m, Math.Abs(densityRatio - 0.22m) / 0.22m));
        var occupied = steps.Select(value => value.StepIndex).Distinct().Count(); var clarity = 1m - Math.Min(1m, report.Collisions / (decimal)Math.Max(1, sourceNotes)); var repetition = 1m - Math.Min(1m, occupied / 64m);
        var gameplay = Percent(0.35m * clarity + 0.30m * density / 100m + 0.20m * repetition + 0.15m * rhythm / 100m);
        return new(similarity, melody, rhythm, bass, gameplay, density);
    }
    private static decimal Percent(decimal value) => decimal.Round(Math.Clamp(value, 0m, 1m) * 100m, 2, MidpointRounding.AwayFromZero);
}
