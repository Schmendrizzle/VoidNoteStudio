using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Domain.Mandachord;

public enum MandachordLayer { Percussion, Bass, Melody }
public enum MandachordPercussionCategory { Kick, Snare, HiHat }
public enum MandachordGenerationPreset { Faithful, Recognizable, Gameplay, RhythmFocus, MelodyFocus }
public enum MandachordStepEditKind { Generated, ManualAdded, ManualModified }

public sealed record MandachordPitch(int Position, int PitchClass, string Name, int PreviewMidiPitch)
{
    public void Validate()
    {
        if (Position is < 0 or > 4 || PitchClass is < 0 or > 11 || PreviewMidiPitch is < 0 or > 127)
            throw new InvalidOperationException("A Mandachord pitch is outside the supported five-position model.");
    }
}

public sealed record MandachordStepProvenance
{
    public Guid? SourceTrackId { get; init; }
    public Guid? SourceEventId { get; init; }
    public string GeneratorVersion { get; init; } = string.Empty;
    public MandachordGenerationPreset? Preset { get; init; }
    public MandachordStepEditKind EditKind { get; set; }
    public List<string> ManualChanges { get; init; } = [];
}

public sealed class MandachordStep : ProjectItem
{
    public int StepIndex { get; set; }
    public MandachordLayer Layer { get; set; }
    public int? PitchPosition { get; set; }
    public MandachordPercussionCategory? PercussionCategory { get; set; }
    public int Velocity { get; set; } = 100;
    public MandachordStepProvenance Provenance { get; init; } = new();
}

public sealed class MandachordPattern : ProjectItem
{
    public string Section { get; set; } = string.Empty;
    public MandachordGenerationPreset Preset { get; set; }
    public string GenerationSource { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<MandachordStep> Steps { get; init; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A Mandachord pattern needs a name.");
        foreach (var step in Steps)
        {
            if (step.StepIndex < 0 || step.StepIndex >= MandachordGridDefinition.Standard.StepCount) throw new InvalidOperationException("Mandachord step is outside the grid.");
            if (step.Velocity is < 1 or > 127) throw new InvalidOperationException("Mandachord velocity is outside MIDI range.");
            if (step.Layer == MandachordLayer.Percussion && step.PercussionCategory is null) throw new InvalidOperationException("Percussion steps need a category.");
            if (step.Layer != MandachordLayer.Percussion && step.PitchPosition is < 0 or > 4) throw new InvalidOperationException("Tonal steps need a valid pitch position.");
        }
        if (Steps.GroupBy(value => (value.StepIndex, value.Layer, value.PitchPosition, value.PercussionCategory)).Any(value => value.Count() > 1))
            throw new InvalidOperationException("A pattern cannot contain duplicate grid positions.");
    }
}

public sealed class MandachordSection : ProjectItem
{
    public MusicalTime Start { get; set; }
    public MusicalTime End { get; set; }
    public Guid PatternId { get; set; }
}

public sealed class MandachordArrangement : ProjectItem
{
    public Guid SelectedSoundSetId { get; set; }
    public MandachordGenerationPreset Preset { get; set; }
    public List<MandachordPattern> Patterns { get; init; } = [];
    public List<MandachordSection> Sections { get; init; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A Mandachord arrangement needs a name.");
        foreach (var pattern in Patterns) pattern.Validate();
        var patternIds = Patterns.Select(value => value.Id).ToHashSet();
        if (Sections.Any(value => value.End.Ticks <= value.Start.Ticks || !patternIds.Contains(value.PatternId)))
            throw new InvalidOperationException("Mandachord sections need a positive range and an arrangement pattern.");
    }
}

public sealed record MandachordVoicePatch(decimal Gain, decimal AttackSeconds, decimal ReleaseSeconds, decimal HarmonicMix);

public sealed class MandachordSoundSet : ProjectItem
{
    public string Description { get; set; } = string.Empty;
    public MandachordVoicePatch Percussion { get; init; } = new(0.7m, 0m, 0.12m, 0.2m);
    public MandachordVoicePatch Bass { get; init; } = new(0.5m, 0.005m, 0.28m, 0.15m);
    public MandachordVoicePatch Melody { get; init; } = new(0.4m, 0.01m, 0.22m, 0.25m);
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A Mandachord sound set needs a name.");
        foreach (var patch in new[] { Percussion, Bass, Melody })
            if (patch.Gain is < 0m or > 1m || patch.AttackSeconds < 0m || patch.ReleaseSeconds <= 0m || patch.HarmonicMix is < 0m or > 1m)
                throw new InvalidOperationException("Mandachord sound patch values are invalid.");
    }
}

public enum MandachordChangeType { Preserved, PitchChanged, TimingChanged, Dropped, CollisionResolved, LayerGenerated }
public sealed record MandachordGenerationChange(Guid? SourceEventId, Guid? OutputStepId, MandachordChangeType Type, string Reason, int? SourcePitch = null, int? TargetPitch = null, MusicalTime? SourceTime = null, int? TargetStep = null);
public sealed record MandachordScores(decimal Similarity, decimal MelodyPreservation, decimal RhythmMatch, decimal BassPreservation, decimal Gameplay, decimal Density);

public sealed class MandachordGenerationReport
{
    public string GeneratorVersion { get; init; } = "1.0.0";
    public MandachordGenerationPreset Preset { get; init; }
    public int SourceNoteCount { get; set; }
    public int PreservedNotes { get; set; }
    public int ShiftedNotes { get; set; }
    public int DroppedNotes { get; set; }
    public int PitchChanges { get; set; }
    public int TimingChanges { get; set; }
    public int Collisions { get; set; }
    public MandachordScores Scores { get; set; } = new(0, 0, 0, 0, 0, 0);
    public List<MandachordGenerationChange> Changes { get; init; } = [];
}
