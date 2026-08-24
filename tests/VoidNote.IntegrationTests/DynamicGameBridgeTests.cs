using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Diagnostic;
using VoidNote.GameBridge.Mapping;
using VoidNote.GameBridge.Playback;
using VoidNote.GameBridge.Profiles;
using VoidNote.GameBridge.Safety;
using VoidNote.Shawzin.Playback;

namespace VoidNote.IntegrationTests;

[Trait("Category", "GameBridge")]
public sealed class DynamicGameBridgeTests
{
    [Fact]
    public async Task CountdownBoundary_DoesNotShiftDynamicScaleOrPhraseTimes()
    {
        var phraseOne = NoteAt(0m);
        var phraseTwo = NoteAt(6m);
        var metrics = new DynamicShawzinQualityMetrics(2, 2, 2, 0, 0, 0, 0m, 100m, 100m, 1, 8);
        var change = new ShawzinScaleChangeEvent(Guid.NewGuid(), new AbsoluteTime(5.470m),
            ShawzinScale.Chromatic, ShawzinScale.PentatonicMajor, 8, "Manual RC regression fixture.",
            20m, 1m, 0.53m, true);
        var fallback = new ShawzinTrack { Scale = ShawzinScale.Chromatic, ShawzinEvents = [phraseOne, phraseTwo] };
        var plan = new DynamicShawzinScalePlan(ShawzinArrangementMode.DynamicIngame, ShawzinScale.Chromatic,
            [new(phraseOne, ShawzinScale.Chromatic, [60], [60]), new(phraseTwo, ShawzinScale.PentatonicMajor, [62], [62])],
            [change], [], metrics, fallback, ShawzinScale.Chromatic, metrics);
        var scheduler = new CapturingScheduler();

        await new DynamicShawzinPlaybackEngine(scheduler, new NoOpDynamicOutput()).PlayAsync(plan);

        Assert.Equal([0m, 5.470m, 6m], scheduler.Targets.Select(value => value.Seconds));
        Assert.Equal(5.470m, plan.ScaleChangeEvents[0].Timestamp.Seconds);
        Assert.Equal(6m, plan.NoteEvents[1].Event.Position.Seconds);
    }

    [Fact]
    public async Task DynamicDiagnostic_EmitsExactTabSequenceBeforeNote()
    {
        var note = new ShawzinEvent(Guid.NewGuid(), new AbsoluteTime(0.01m),
            new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]));
        var fallback = new ShawzinTrack { Scale = ShawzinScale.Minor, ShawzinEvents = [note] };
        var metrics = new DynamicShawzinQualityMetrics(1, 1, 1, 0, 0, 0, 0m, 100m, 100m, 1, 6);
        var change = new ShawzinScaleChangeEvent(Guid.NewGuid(), AbsoluteTime.Zero, ShawzinScale.Minor, ShawzinScale.Chromatic,
            6, "Prevents synthetic fixture substitutions.", 20m, 1m, 0.01m, true);
        var plan = new DynamicShawzinScalePlan(ShawzinArrangementMode.DynamicIngame, ShawzinScale.Minor,
            [new(note, ShawzinScale.Chromatic, [60], [60])], [change], [], metrics, fallback, ShawzinScale.Minor, metrics);
        await using var real = new ThrowOnInputBridge();
        await using var session = new GameBridgePlaybackSession(real, new ShawzinInputMapper(), new KeybindProfileValidator(),
            new Focus(), new GameBridgeArmController(), new SystemGameBridgeStartDelay());
        var timing = new GameBridgeTimingOptions(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.Zero)
        {
            ScaleKeyPressDuration = TimeSpan.Zero + TimeSpan.FromMilliseconds(1),
            ScaleKeyReleaseDelay = TimeSpan.Zero,
            MinimumGapBeforeNextNote = TimeSpan.Zero,
        };

        var result = await session.DryRunDynamicAsync(plan, ShawzinKeybindProfile.CreateDefault(), timing);

        Assert.Empty(result.MappingErrors);
        Assert.Equal(6, result.Diagnostics.TotalScaleKeyPresses);
        Assert.Equal(12, result.InputLog.Count(value => value.Key.Name == "Tab"));
        Assert.Equal(14, result.InputCount);
        Assert.All(result.InputLog.Take(12), value => Assert.Equal("Tab", value.Key.Name));
        var diagnostic = Assert.Single(result.Diagnostics.ScaleChanges);
        Assert.Equal(ShawzinScale.Minor, diagnostic.SourceScale);
        Assert.Equal(ShawzinScale.Chromatic, diagnostic.TargetScale);
        Assert.True(diagnostic.TimingSafe);
    }

    private sealed class Focus : IGameTargetFocusService
    {
        public TargetFocusStatus GetStatus(string targetWindowTitle) => new(true, true, "Focused");
    }

    private static ShawzinEvent NoteAt(decimal seconds) => new(Guid.NewGuid(), new AbsoluteTime(seconds),
        new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]));

    private sealed class CapturingScheduler : IShawzinPlaybackScheduler
    {
        public List<AbsoluteTime> Targets { get; } = [];
        public long GetTimestamp() => 1;
        public AbsoluteTime GetElapsedTime(long startingTimestamp) => AbsoluteTime.Zero;
        public ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken)
        { Targets.Add(targetOffset); return ValueTask.CompletedTask; }
    }

    private sealed class NoOpDynamicOutput : IDynamicShawzinPlaybackOutput
    {
        public ValueTask ChangeScaleAsync(ShawzinScaleChangeEvent scaleChange, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask PlayNoteAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask PlayChordAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask PositionChangedAsync(AbsoluteTime position, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class ThrowOnInputBridge : IGameInputBridge
    {
        public GameInputCapability Capability => new(true, "Guard", "Throws if real input is used.");
        private static ValueTask Fail() => ValueTask.FromException(new InvalidOperationException("Real bridge was used."));
        public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
