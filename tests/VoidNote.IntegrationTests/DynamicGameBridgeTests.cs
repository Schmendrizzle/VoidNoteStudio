using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Diagnostic;
using VoidNote.GameBridge.Mapping;
using VoidNote.GameBridge.Playback;
using VoidNote.GameBridge.Profiles;
using VoidNote.GameBridge.Safety;

namespace VoidNote.IntegrationTests;

[Trait("Category", "GameBridge")]
public sealed class DynamicGameBridgeTests
{
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
            new Focus(), new GameBridgeArmController());
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
