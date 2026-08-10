namespace VoidNote.Domain.Mandachord;

/// <summary>Single data source for the documented four-bar Mandachord grid.</summary>
public sealed record MandachordGridDefinition(
    int Bars, int BeatsPerBar, int StepsPerBeat,
    IReadOnlyList<MandachordPitch> MelodyPitches,
    IReadOnlyList<MandachordPitch> BassPitches,
    IReadOnlyList<MandachordPercussionCategory> PercussionCategories)
{
    public int StepsPerBar => BeatsPerBar * StepsPerBeat;
    public int StepCount => Bars * StepsPerBar;
    public decimal LoopBeats => Bars * BeatsPerBar;
    public string TimingResolution => "1/16 note";

    public static MandachordGridDefinition Standard { get; } = new(
        4, 4, 4,
        [new(0, 2, "D", 62), new(1, 5, "F", 65), new(2, 7, "G", 67), new(3, 9, "A", 69), new(4, 0, "C", 72)],
        [new(0, 2, "D", 38), new(1, 5, "F", 41), new(2, 7, "G", 43), new(3, 9, "A", 45), new(4, 0, "C", 48)],
        Enum.GetValues<MandachordPercussionCategory>());

    public void Validate()
    {
        if (Bars != 4 || BeatsPerBar != 4 || StepsPerBeat != 4 || StepCount != 64)
            throw new InvalidOperationException("The standard Mandachord definition must describe four 4/4 bars at sixteenth-note resolution.");
        if (MelodyPitches.Count != 5 || BassPitches.Count != 5 || PercussionCategories.Count != 3)
            throw new InvalidOperationException("The standard Mandachord rows are incomplete.");
        foreach (var pitch in MelodyPitches.Concat(BassPitches)) pitch.Validate();
    }
}

public static class BuiltInMandachordSoundSets
{
    public static MandachordSoundSet SyntheticDefault() => new()
    {
        Id = Guid.Parse("5dba5ca5-27ef-4e4b-97a7-673673e40001"),
        Name = "VoidNote Synthetic",
        Description = "Original sine/noise preview voices; contains no Warframe assets.",
    };
}
