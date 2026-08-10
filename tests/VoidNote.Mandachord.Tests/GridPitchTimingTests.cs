using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Mandachord.Mapping;

namespace VoidNote.Mandachord.Tests;

public sealed class GridPitchTimingTests
{
    [Fact] public void StandardGrid_IsFourBarsSixtyFourSixteenthStepsAndDataDriven()
    {
        var grid = MandachordGridDefinition.Standard; grid.Validate();
        Assert.Equal(4, grid.Bars); Assert.Equal(16, grid.StepsPerBar); Assert.Equal(64, grid.StepCount); Assert.Equal(16, grid.LoopBeats); Assert.Equal(5, grid.MelodyPitches.Count); Assert.Equal(3, grid.PercussionCategories.Count);
    }
    [Theory]
    [InlineData(62, MandachordPitchMappingKind.Exact, 0)]
    [InlineData(74, MandachordPitchMappingKind.OctaveShift, -12)]
    [InlineData(61, MandachordPitchMappingKind.TranspositionPreferred, 1)]
    [InlineData(1, MandachordPitchMappingKind.NotMeaningful, 0)]
    public void PitchMapping_ClassifiesWithoutSilentChange(int pitch, MandachordPitchMappingKind kind, int delta)
    {
        var result = new MandachordPitchMapper().Map(pitch, MandachordLayer.Melody);
        Assert.Equal(kind, result.Kind); Assert.Equal(delta, result.SemitoneChange); Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
    [Fact] public void TranspositionRanking_IsStableAndPrefersCoverageThenDistance() => Assert.Equal(0, new MandachordPitchMapper().FindTranspositions([62, 65, 67], MandachordLayer.Melody)[0].SuggestedTransposition);
    [Fact] public void TimingMapping_QuantizesIndependentlyWrapsAndReportsCollision()
    {
        var timeline = ProjectTimeline.CreateDefault(); var events = new[] { Note(239, 60), Note(241, 62), Note(15_600, 64) };
        var result = new MandachordTimingMapper().Map(timeline, events, MusicalTime.Zero);
        Assert.Equal(1, result[0].StepIndex); Assert.Equal(1, result[1].StepIndex); Assert.True(result[0].Collision); Assert.Equal(1, result[2].StepIndex);
        Assert.Equal(timeline.FromBeats(0.25m), result[0].QuantizedTime);
    }
    [Fact] public void LongTimingMapping_HasNoCumulativeDrift()
    {
        var timeline = ProjectTimeline.CreateDefault(); var notes = Enumerable.Range(0, 1000).Select(i => Note(i * 240L + (i % 2), 60)).ToArray();
        var result = new MandachordTimingMapper().Map(timeline, notes, MusicalTime.Zero);
        Assert.All(result, value => Assert.InRange(Math.Abs(value.TimingErrorSteps), 0m, 0.01m));
    }
    private static MusicalEvent Note(long tick, int pitch) => new(Guid.NewGuid(), new(tick), new(240), pitch, 100, MusicalEventSource.ImportedMidi, 1m);
}
