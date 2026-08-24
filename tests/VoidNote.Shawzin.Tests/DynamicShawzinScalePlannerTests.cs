using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Dynamic;
using VoidNote.Shawzin.Mapping;
using VoidNote.Shawzin.Preview;

namespace VoidNote.Shawzin.Tests;

public sealed class DynamicShawzinScalePlannerTests
{
    private static readonly ProjectTimeline Timeline = ProjectTimeline.CreateDefault();
    private readonly DynamicShawzinScalePlanner _planner = new(new ShawzinArranger(new ShawzinPitchMapper()));

    [Fact]
    public void ScaleCycle_CurrentToNext_IsOnePress() =>
        Assert.Equal(1, WarframeShawzinScaleCycle.RequiredForwardPresses(ShawzinScale.Minor, ShawzinScale.Hirajoshi));

    [Fact]
    public void ScaleCycle_CurrentToPrevious_WrapsWithEightPresses() =>
        Assert.Equal(8, WarframeShawzinScaleCycle.RequiredForwardPresses(ShawzinScale.Minor, ShawzinScale.Major));

    [Fact]
    public void ScaleCycle_SameScale_IsZeroPresses() =>
        Assert.Equal(0, WarframeShawzinScaleCycle.RequiredForwardPresses(ShawzinScale.Chromatic, ShawzinScale.Chromatic));

    [Fact]
    public void Planner_UsesSafePauseForBeneficialScaleChange()
    {
        var plan = Plan(TwoScaleFixture(3m));

        var change = Assert.Single(plan.ScaleChangeEvents);
        Assert.True(change.IsTimingSafe);
        Assert.True(change.AvailableWindowSeconds >= change.RequiredWindowSeconds);
        Assert.Equal(ShawzinScale.Minor, change.SourceScale);
        Assert.Equal(ShawzinScale.Chromatic, change.TargetScale);
        Assert.Equal(6, change.RequiredScaleKeyPressCount);
    }

    [Fact]
    public void Planner_RejectsScaleChangeWhenPauseIsTooShort()
    {
        var settings = Permissive() with { PhraseGapSeconds = 0.05m, ScaleKeyPressDurationSeconds = 0.08m, ScaleKeyReleaseDelaySeconds = 0.08m };
        var plan = _planner.Plan(TwoScaleFixture(1.08m), Timeline, BuiltInShawzinDefinitions.Default,
            [ShawzinScale.Minor, ShawzinScale.Chromatic], settings);

        Assert.Empty(plan.ScaleChangeEvents);
        Assert.Equal(ShawzinArrangementMode.ShareCode, plan.Mode);
    }

    [Fact]
    public void Planner_AvoidsUnnecessaryChanges()
    {
        var track = Track((0m, 60), (0.3m, 62), (0.6m, 63), (0.9m, 65), (3m, 67), (3.3m, 68), (3.6m, 70), (3.9m, 72));
        var plan = Plan(track);

        Assert.Empty(plan.ScaleChangeEvents);
        Assert.Equal(ShawzinArrangementMode.ShareCode, plan.Mode);
        Assert.Equal(ShawzinScale.Minor, plan.RequiredInitialScale);
    }

    [Fact]
    public void Planner_PrefersFixedScaleWhenDynamicBenefitIsBelowThreshold()
    {
        var track = Track((0m, 60), (0.3m, 62), (0.6m, 63), (0.9m, 65), (3m, 67), (3.3m, 68), (3.6m, 70), (3.9m, 71));
        var plan = _planner.Plan(track, Timeline, BuiltInShawzinDefinitions.Default,
            [ShawzinScale.Minor, ShawzinScale.Major], Permissive() with { ImprovementThreshold = 50m });

        Assert.Equal(ShawzinArrangementMode.ShareCode, plan.Mode);
        Assert.Empty(plan.ScaleChangeEvents);
        Assert.True(plan.CanExportClassicShareCode);
    }

    [Fact]
    public void Planner_ChoosesDynamicForLargePitchErrorReduction()
    {
        var plan = Plan(TwoScaleFixture(3m));
        Assert.Equal(ShawzinArrangementMode.DynamicIngame, plan.Mode);
        Assert.True(plan.Metrics.MusicalSimilarityPercent > plan.FixedScaleMetrics.MusicalSimilarityPercent);
        Assert.True(plan.Metrics.MeanPitchErrorSemitones < plan.FixedScaleMetrics.MeanPitchErrorSemitones);
        Assert.False(plan.CanExportClassicShareCode);
    }

    [Fact]
    public void DynamicPreview_ReconstructsPitchFromActiveScale()
    {
        var input = new ShawzinNote(ShawzinString.First, ShawzinFret.None);
        var first = new ShawzinEvent(Guid.NewGuid(), AbsoluteTime.Zero, new ShawzinChord([input]));
        var second = new ShawzinEvent(Guid.NewGuid(), new AbsoluteTime(0.5m), new ShawzinChord([input]));
        var fallback = new ShawzinTrack { Scale = ShawzinScale.Minor, ShawzinEvents = [first, second] };
        var metrics = new DynamicShawzinQualityMetrics(2, 2, 2, 0, 0, 0, 0m, 100m, 100m, 1, 3);
        var plan = new DynamicShawzinScalePlan(ShawzinArrangementMode.DynamicIngame, ShawzinScale.Minor,
            [new(first, ShawzinScale.Minor, [60], [60]), new(second, ShawzinScale.Yo, [61], [61])],
            [new(Guid.NewGuid(), new AbsoluteTime(0.25m), ShawzinScale.Minor, ShawzinScale.Yo, 3, "Preview fixture", 10m, 0.4m, 0.2m, true)],
            [], metrics, fallback, ShawzinScale.Minor, metrics);

        var audio = new SyntheticDynamicShawzinPreviewRenderer().Render(plan, BuiltInShawzinDefinitions.Default);
        var firstCrossings = ZeroCrossings(audio.WaveData, 0, 0.30m, audio.SampleRate);
        var secondCrossings = ZeroCrossings(audio.WaveData, 0.5m, 0.30m, audio.SampleRate);

        Assert.True(secondCrossings > firstCrossings, $"Expected the Yo-scale C# to have more zero crossings than the Minor-scale C ({firstCrossings} vs {secondCrossings}).");
    }

    [Fact]
    public void ClassicEncoder_SurfaceCannotReceiveScaleChangeEvents()
    {
        var method = typeof(IShawzinCodeEncoder).GetMethod(nameof(IShawzinCodeEncoder.Encode));
        Assert.Equal(typeof(ShawzinSong), Assert.Single(method!.GetParameters()).ParameterType);
        Assert.DoesNotContain(typeof(WarframeShawzinCodeEncoder).GetMethods(), value =>
            value.GetParameters().Any(parameter => parameter.ParameterType == typeof(ShawzinScaleChangeEvent)));
    }

    [Fact]
    public void FixedShareCode_GoldenOutputRemainsUnchanged()
    {
        var track = new ShawzinTrack
        {
            Scale = ShawzinScale.Chromatic,
            ShawzinEvents = [new(Guid.Parse("10000000-0000-0000-0000-000000000001"), AbsoluteTime.Zero,
                new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]))],
        };
        Assert.Equal("3BAA", new WarframeShawzinCodeEncoder().Encode(new ShawzinSong(track)).Code);
    }

    private DynamicShawzinScalePlan Plan(MidiTrack track) => _planner.Plan(track, Timeline, BuiltInShawzinDefinitions.Default,
        [ShawzinScale.Minor, ShawzinScale.Chromatic], Permissive());

    private static DynamicShawzinScalePlanningSettings Permissive() => new()
    {
        InitialScale = ShawzinScale.Minor,
        MinimumSectionDurationSeconds = 0.5m,
        MinimumNotesBeforeChange = 3,
        ImprovementThreshold = 0.5m,
        MinimumPitchErrorsPrevented = 1,
        MinimumSubstitutionsPrevented = 1,
    };

    private static MidiTrack TwoScaleFixture(decimal secondSectionStart) => Track(
        (0m, 72), (0.3m, 74), (0.6m, 75), (0.9m, 77),
        (secondSectionStart, 61), (secondSectionStart + 0.3m, 64), (secondSectionStart + 0.6m, 66), (secondSectionStart + 0.9m, 69));

    private static MidiTrack Track(params (decimal Seconds, int Pitch)[] notes) => new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Name = "Synthetic dynamic-scale regression",
        Events = notes.Select((value, index) => new MusicalEvent(
            Guid.Parse($"30000000-0000-0000-0000-{index + 1:x12}"),
            Timeline.ToMusicalTime(new AbsoluteTime(value.Seconds)), Timeline.ToMusicalTime(new AbsoluteTime(0.1m)),
            value.Pitch, 100, MusicalEventSource.Generated, 1m)).ToList(),
    };

    private static int ZeroCrossings(byte[] wave, decimal startSeconds, decimal durationSeconds, int sampleRate)
    {
        var start = 44 + (int)(startSeconds * sampleRate) * 2;
        var end = Math.Min(wave.Length - 2, start + (int)(durationSeconds * sampleRate) * 2);
        var crossings = 0;
        var previous = BitConverter.ToInt16(wave, start);
        for (var offset = start + 2; offset <= end; offset += 2)
        {
            var current = BitConverter.ToInt16(wave, offset);
            if (previous < 0 && current >= 0 || previous > 0 && current <= 0) crossings++;
            previous = current;
        }
        return crossings;
    }
}
