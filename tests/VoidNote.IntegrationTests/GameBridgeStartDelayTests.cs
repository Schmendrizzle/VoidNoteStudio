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
public sealed class GameBridgeStartDelayTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task SelectedDelay_IsPassedToCountdown(int seconds)
    {
        var delay = new ImmediateDelay();
        await using var bridge = new DiagnosticGameInputBridge();
        await using var session = Session(bridge, new TrackingFocus(true), delay);
        session.Arm(true);

        await session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(seconds), null);

        Assert.Equal(TimeSpan.FromSeconds(seconds), delay.RequestedDelay);
    }

    [Fact]
    public async Task FocusIsCheckedOnlyAfterCountdownCompletes()
    {
        var delay = new GatedDelay();
        var focus = new TrackingFocus(true);
        await using var bridge = new DiagnosticGameInputBridge();
        await using var session = Session(bridge, focus, delay);
        session.Arm(true);

        var playback = session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), null);
        await delay.Started;

        Assert.Equal(0, focus.CheckCount);
        Assert.Empty(bridge.Events);

        delay.Complete();
        await playback;

        Assert.True(focus.CheckCount >= 1);
        Assert.NotEmpty(bridge.Events);
    }

    [Fact]
    public async Task WrongFocusAfterCountdown_AbortsDisarmsAndReleasesAll()
    {
        var bridge = new TrackingBridge();
        await using var session = Session(bridge, new TrackingFocus(false), new ImmediateDelay());
        session.Arm(true);

        await Assert.ThrowsAsync<GameBridgeFocusException>(() => session.PlayAsync(
            Track(), Profile(), Timing(), "Warframe", true, TimeSpan.FromSeconds(5), null));

        Assert.Equal(GameBridgeArmState.Disarmed, session.ArmState);
        Assert.Empty(bridge.Transitions);
        Assert.True(bridge.ReleaseAllCount > 0);
    }

    [Fact]
    public async Task StopDuringCountdown_SendsNoInputAndReturnsSafely()
    {
        var delay = new GatedDelay();
        var bridge = new TrackingBridge();
        await using var session = Session(bridge, new TrackingFocus(true), delay);
        session.Arm(true);
        var playback = session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), null);
        await delay.Started;

        await session.StopAsync();
        await playback;

        Assert.Empty(bridge.Transitions);
        Assert.Equal(GameBridgeArmState.Disarmed, session.ArmState);
        Assert.True(bridge.ReleaseAllCount > 0);
    }

    [Fact]
    public async Task EmergencyStopDuringCountdown_SendsNoInputDisarmsAndReleasesAll()
    {
        var delay = new GatedDelay();
        var bridge = new TrackingBridge();
        await using var session = Session(bridge, new TrackingFocus(true), delay);
        session.Arm(true);
        var playback = session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), null);
        await delay.Started;

        await session.EmergencyStopAsync();
        await playback;

        Assert.Empty(bridge.Transitions);
        Assert.Equal(GameBridgeArmState.Disarmed, session.ArmState);
        Assert.True(bridge.ReleaseAllCount > 0);
    }

    [Fact]
    public async Task CountdownItselfNeverSendsSyntheticInput()
    {
        var delay = new GatedDelay();
        var bridge = new TrackingBridge();
        await using var session = Session(bridge, new TrackingFocus(true), delay);
        session.Arm(true);
        var playback = session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), null);
        await delay.Started;

        Assert.Empty(bridge.Transitions);
        Assert.Equal(0, bridge.ReleaseAllCount);

        await session.StopAsync();
        await playback;
    }

    [Fact]
    public async Task InitialFocusCheckPrecedesFirstInputAndTimelineStart()
    {
        var order = new List<string>();
        var delay = new ImmediateDelay(order);
        var focus = new TrackingFocus(true, order);
        var bridge = new TrackingBridge(order);
        await using var session = Session(bridge, focus, delay);
        session.Arm(true);

        await session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), null);

        Assert.Equal("delay-complete", order[0]);
        Assert.Equal("focus", order[1]);
        Assert.True(order.IndexOf("input") > order.IndexOf("focus"));
    }

    [Fact]
    public async Task CountdownProgress_HasDeterministicSecondsAndFinalBarState()
    {
        var progress = new CapturingProgress();
        var delay = new ImmediateDelay();
        await using var bridge = new DiagnosticGameInputBridge();
        await using var session = Session(bridge, new TrackingFocus(true), delay);
        session.Arm(true);

        await session.PlayAsync(Track(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), progress);

        var countdown = progress.Values.Where(value => value.Phase == GameBridgeStartPhase.Countdown).ToArray();
        Assert.Equal([5, 4, 3, 2, 1, 0], countdown.Select(value => value.RemainingSeconds));
        Assert.Equal(1d, progress.Values[^1].Completion);
        Assert.Equal(GameBridgeStartPhase.Ready, progress.Values[^1].Phase);
    }

    [Fact]
    public async Task DynamicPlayback_UsesTheSameCountdownBeforeAnyInput()
    {
        var delay = new GatedDelay();
        var bridge = new TrackingBridge();
        await using var session = Session(bridge, new TrackingFocus(true), delay);
        session.Arm(true);
        var playback = session.PlayDynamicAsync(DynamicPlan(), Profile(), Timing(), "Warframe", true,
            TimeSpan.FromSeconds(5), null);
        await delay.Started;
        Assert.Empty(bridge.Transitions);

        delay.Complete();
        await playback;

        Assert.Equal(TimeSpan.FromSeconds(5), delay.RequestedDelay);
        Assert.NotEmpty(bridge.Transitions);
    }

    [Fact]
    public async Task DryRunBypassesCountdownAndFocus()
    {
        var delay = new ThrowingDelay();
        await using var bridge = new DiagnosticGameInputBridge();
        await using var session = Session(bridge, new ThrowingFocus(), delay);

        var result = await session.DryRunAsync(Track(), Profile(), Timing());

        Assert.Equal(1, result.EventCount);
        Assert.False(delay.WasCalled);
    }

    private static GameBridgePlaybackSession Session(IGameInputBridge bridge, IGameTargetFocusService focus, IGameBridgeStartDelay delay) =>
        new(bridge, new ShawzinInputMapper(), new KeybindProfileValidator(), focus, new GameBridgeArmController(), delay);
    private static ShawzinKeybindProfile Profile() => ShawzinKeybindProfile.CreateDefault();
    private static GameBridgeTimingOptions Timing() => new(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.Zero)
    {
        ScaleKeyPressDuration = TimeSpan.FromMilliseconds(1),
        ScaleKeyReleaseDelay = TimeSpan.Zero,
        MinimumGapBeforeNextNote = TimeSpan.Zero,
    };
    private static ShawzinTrack Track() => new()
    {
        ShawzinEvents = [new(Guid.NewGuid(), AbsoluteTime.Zero,
            new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]))],
    };
    private static DynamicShawzinScalePlan DynamicPlan()
    {
        var note = Track().ShawzinEvents[0];
        var metrics = new DynamicShawzinQualityMetrics(1, 1, 1, 0, 0, 0, 0m, 100m, 100m, 1, 0);
        var fallback = new ShawzinTrack { Scale = ShawzinScale.Chromatic, ShawzinEvents = [note] };
        return new(ShawzinArrangementMode.DynamicIngame, ShawzinScale.Chromatic,
            [new(note, ShawzinScale.Chromatic, [60], [60])], [], [], metrics, fallback, ShawzinScale.Chromatic, metrics);
    }

    private sealed class ImmediateDelay(List<string>? order = null) : IGameBridgeStartDelay
    {
        public TimeSpan RequestedDelay { get; private set; }
        public Task WaitAsync(TimeSpan delay, IProgress<GameBridgeStartProgress>? progress, CancellationToken cancellationToken)
        {
            RequestedDelay = delay;
            for (var seconds = (int)delay.TotalSeconds; seconds >= 0; seconds--)
                progress?.Report(new(GameBridgeStartPhase.Countdown, delay, TimeSpan.FromSeconds(seconds),
                    delay == TimeSpan.Zero ? 1d : 1d - seconds / delay.TotalSeconds));
            order?.Add("delay-complete");
            return Task.CompletedTask;
        }
    }

    private sealed class GatedDelay : IGameBridgeStartDelay
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Started => _started.Task;
        public TimeSpan RequestedDelay { get; private set; }
        public void Complete() => _completion.TrySetResult();
        public async Task WaitAsync(TimeSpan delay, IProgress<GameBridgeStartProgress>? progress, CancellationToken cancellationToken)
        {
            RequestedDelay = delay;
            progress?.Report(new(GameBridgeStartPhase.Countdown, delay, delay, 0d));
            _started.TrySetResult();
            await _completion.Task.WaitAsync(cancellationToken);
            progress?.Report(new(GameBridgeStartPhase.Countdown, delay, TimeSpan.Zero, 1d));
        }
    }

    private sealed class ThrowingDelay : IGameBridgeStartDelay
    {
        public bool WasCalled { get; private set; }
        public Task WaitAsync(TimeSpan delay, IProgress<GameBridgeStartProgress>? progress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Countdown must not be used by dry run.");
        }
    }

    private sealed class CapturingProgress : IProgress<GameBridgeStartProgress>
    {
        public List<GameBridgeStartProgress> Values { get; } = [];
        public void Report(GameBridgeStartProgress value) => Values.Add(value);
    }

    private sealed class TrackingFocus(bool focused, List<string>? order = null) : IGameTargetFocusService
    {
        public int CheckCount { get; private set; }
        public TargetFocusStatus GetStatus(string targetWindowTitle)
        {
            CheckCount++;
            order?.Add("focus");
            return new(true, focused, focused ? "Focused" : "Focused window is 'VoidNote Studio'.");
        }
    }

    private sealed class ThrowingFocus : IGameTargetFocusService
    {
        public TargetFocusStatus GetStatus(string targetWindowTitle) => throw new InvalidOperationException("Dry run must not check focus.");
    }

    private sealed class TrackingBridge(List<string>? order = null) : IGameInputBridge
    {
        private readonly HashSet<GameInputKey> _held = [];
        public GameInputCapability Capability => new(true, "Test", "Test bridge");
        public List<GameInputTransition> Transitions { get; } = [];
        public int ReleaseAllCount { get; private set; }
        public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            order?.Add("input");
            _held.Add(key); Transitions.Add(GameInputTransition.KeyDown); return ValueTask.CompletedTask;
        }
        public async ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default)
        { foreach (var key in keys) await PressKeyAsync(key, eventId, cancellationToken); }
        public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
        { _held.Remove(key); Transitions.Add(GameInputTransition.KeyUp); return ValueTask.CompletedTask; }
        public async ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default)
        { await PressKeyAsync(key, eventId, cancellationToken); await ReleaseKeyAsync(key, eventId, cancellationToken); }
        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default)
        {
            ReleaseAllCount++;
            foreach (var key in _held.ToArray()) { _held.Remove(key); Transitions.Add(GameInputTransition.KeyUp); }
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
