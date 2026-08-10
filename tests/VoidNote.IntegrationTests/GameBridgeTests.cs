using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Diagnostic;
using VoidNote.GameBridge.Mapping;
using VoidNote.GameBridge.Playback;
using VoidNote.GameBridge.Profiles;
using VoidNote.GameBridge.Safety;
using VoidNote.Infrastructure.Files;
using VoidNote.Midi;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Mapping;
using VoidNote.Shawzin.Playback;

namespace VoidNote.IntegrationTests;

[Trait("Category", "GameBridge")]
public sealed class GameBridgeTests
{
    private readonly ShawzinKeybindProfile _profile = ShawzinKeybindProfile.CreateDefault();
    private readonly ShawzinInputMapper _mapper = new();

    [Fact]
    public void SingleNote_MapsFretBeforeCorrectString()
    {
        var action = _mapper.Map(Event(0m, ShawzinFret.Sky, ShawzinString.Second), _profile);
        Assert.Equal(["Left"], action.FretKeys.Select(x => x.Name));
        Assert.Equal(["2"], action.StringKeys.Select(x => x.Name));
    }

    [Theory]
    [InlineData(ShawzinFret.None, new string[0])]
    [InlineData(ShawzinFret.Earth, new[] { "Down" })]
    [InlineData(ShawzinFret.Sky | ShawzinFret.Water, new[] { "Left", "Right" })]
    public void Frets_MapDeterministically(ShawzinFret frets, string[] expected) =>
        Assert.Equal(expected, _mapper.Map(Event(0m, frets, ShawzinString.First), _profile).FretKeys.Select(x => x.Name));

    [Fact]
    public async Task Chord_PressesAllInputsAndReleasesEverything()
    {
        await using var bridge = new DiagnosticGameInputBridge();
        var output = Output(bridge);
        output.Begin();
        await output.PlayChordAsync(Event(0m, ShawzinFret.Sky | ShawzinFret.Earth, ShawzinString.First, ShawzinString.Third), default);
        Assert.Equal(["Left", "Down", "1", "3"], bridge.Events.Where(x => x.Transition == GameInputTransition.KeyDown).Select(x => x.Key.Name));
        Assert.Empty(bridge.HeldKeys);
        Assert.Equal(8, output.Diagnostics.InputCount);
    }

    [Fact]
    public void InvalidProfile_ReportsMissingInvalidAndConflictingBindings()
    {
        var profile = _profile with { Name = "", Bindings = new Dictionary<ShawzinInputBinding, string> { [ShawzinInputBinding.String1] = "?", [ShawzinInputBinding.String2] = "?" } };
        var result = new KeybindProfileValidator().Validate(profile);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == "MissingBinding");
        Assert.Contains(result.Issues, x => x.Code == "InvalidKey");
        Assert.Contains(result.Issues, x => x.Code == "BindingConflict");
    }

    [Fact]
    public async Task Profiles_SaveLoadDuplicateAndDelete()
    {
        using var directory = new TemporaryDirectory();
        var validator = new KeybindProfileValidator();
        var service = new KeybindProfileService(new JsonKeybindProfileStore(new AppPathProvider(directory.Path), validator), validator);
        var loaded = await service.LoadAsync(); Assert.Single(loaded);
        var duplicated = await service.DuplicateAsync(loaded, loaded[0].Id, "Custom"); Assert.Equal(2, duplicated.Count);
        var reloaded = await service.LoadAsync(); Assert.Contains(reloaded, x => x.Name == "Custom");
        var deleted = await service.DeleteAsync(reloaded, reloaded.Single(x => x.Name == "Custom").Id); Assert.Single(deleted);
    }

    [Fact]
    public async Task FocusLoss_AbortsWithoutSendingInput()
    {
        await using var bridge = new DiagnosticGameInputBridge();
        var output = new GameBridgePlaybackOutput(bridge, _mapper, _profile, new Focus(false), "Warframe", true, FastTiming()); output.Begin();
        await Assert.ThrowsAsync<GameBridgeFocusException>(async () => await output.PlayNoteAsync(Event(0m, ShawzinFret.None, ShawzinString.First), default));
        Assert.Empty(bridge.Events); Assert.Equal(1, output.Diagnostics.FocusLosses); Assert.Equal(1, output.Diagnostics.AbortedEvents);
    }

    [Fact]
    public async Task InputFailure_ReleasesHeldKeys()
    {
        await using var bridge = new FailingBridge();
        var output = Output(bridge); output.Begin();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await output.PlayNoteAsync(Event(0m, ShawzinFret.Sky, ShawzinString.First), default));
        Assert.True(bridge.ReleaseAllCalled);
    }

    [Fact]
    public async Task InputFailure_StopsAndDisarmsSession()
    {
        await using var bridge = new FailingBridge();
        var arm = new GameBridgeArmController();
        await using var session = Session(bridge, arm); session.Arm(true);
        var track = new ShawzinTrack { ShawzinEvents = [Event(0m, ShawzinFret.Sky, ShawzinString.First)] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PlayAsync(track, _profile, FastTiming(), "Warframe", true));
        Assert.Equal(GameBridgeArmState.Disarmed, session.ArmState); Assert.True(bridge.ReleaseAllCalled);
    }

    [Fact]
    public async Task CancellationAndStop_ReleaseAllKeys()
    {
        await using var bridge = new DiagnosticGameInputBridge();
        await bridge.PressKeyAsync(new("A"));
        await bridge.ReleaseAllAsync();
        Assert.Empty(bridge.HeldKeys);
        Assert.Equal(GameInputTransition.KeyUp, bridge.Events[^1].Transition);
    }

    [Fact]
    public async Task ArmEmergencyStop_DisarmsAndReleasesAll()
    {
        await using var bridge = new DiagnosticGameInputBridge();
        var arm = new GameBridgeArmController();
        await using var session = Session(bridge, arm);
        Assert.Throws<InvalidOperationException>(() => session.Arm(false));
        session.Arm(true); await bridge.PressKeyAsync(new("A")); await session.EmergencyStopAsync();
        Assert.Equal(GameBridgeArmState.Disarmed, session.ArmState); Assert.Empty(bridge.HeldKeys);
    }

    [Fact]
    public async Task Stop_CancelsWaitingSchedulerAndDisarms()
    {
        await using var bridge = new DiagnosticGameInputBridge();
        var arm = new GameBridgeArmController();
        await using var session = Session(bridge, arm); session.Arm(true);
        var play = session.PlayAsync(new ShawzinTrack { ShawzinEvents = [Event(10m, ShawzinFret.None, ShawzinString.First)] }, _profile, FastTiming(), "Warframe", true);
        await Task.Delay(20); await session.StopAsync(); await play;
        Assert.Equal(GameBridgeArmState.Disarmed, session.ArmState); Assert.Empty(bridge.HeldKeys); Assert.Empty(bridge.Events);
    }

    [Fact]
    public async Task KeyDownLead_AdjustsEachAbsoluteTargetWithoutCumulativeTiming()
    {
        await using var bridge = new DiagnosticGameInputBridge();
        var output = new GameBridgePlaybackOutput(bridge, _mapper, _profile, new Focus(true), "Warframe", true,
            new(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(1), TimeSpan.Zero)); output.Begin();
        var scheduler = new CapturingScheduler();
        await using var engine = new ShawzinPlaybackEngine(scheduler, output);
        await engine.LoadAsync(new ShawzinTrack { ShawzinEvents = [Event(1m, ShawzinFret.None, ShawzinString.First), Event(2m, ShawzinFret.None, ShawzinString.Second)] });
        await engine.PlayAsync();
        Assert.Equal([0.995m, 1.995m], scheduler.Targets.Select(x => x.Seconds));
    }

    [Fact]
    public async Task DryRun_IsDeterministicAndNeverUsesRealInput()
    {
        await using var real = new ThrowOnInputBridge();
        await using var session = Session(real, new GameBridgeArmController());
        var track = new ShawzinTrack { ShawzinEvents = [Event(0m, ShawzinFret.Water, ShawzinString.First, ShawzinString.Second)] };
        var result = await session.DryRunAsync(track, _profile, FastTiming());
        Assert.Equal(1, result.EventCount); Assert.Equal(6, result.InputCount); Assert.Empty(result.MappingErrors);
        Assert.Equal(["Right", "1", "2", "2", "1", "Right"], result.InputLog.Select(x => x.Key.Name));
    }

    [Fact]
    public async Task MidiToDiagnosticBridge_EndToEndPreservesOrderAndReleasesAll()
    {
        await using var midi = SingleNoteMidi();
        var imported = await new DryWetMidiFileImporter().ImportAsync(midi);
        var arrangement = new ShawzinArranger(new ShawzinPitchMapper()).Arrange(Assert.Single(imported.Tracks), imported.Timeline,
            BuiltInShawzinDefinitions.Default, new ArrangementOptions { Scale = ShawzinScale.Chromatic, Strategies = ArrangementStrategy.Strict });
        var encoded = new WarframeShawzinCodec().Encode(new ShawzinSong(arrangement.Track!)); Assert.True(encoded.IsSuccess);
        await using var bridge = new DiagnosticGameInputBridge();
        var output = Output(bridge); output.Begin();
        await using var engine = new ShawzinPlaybackEngine(new ImmediateScheduler(), output);
        await engine.LoadAsync(arrangement.Track!); await engine.PlayAsync();
        Assert.Single(output.Diagnostics.Events); Assert.True(output.Diagnostics.Events[0].InputCount >= 2);
        Assert.Empty(bridge.HeldKeys); Assert.Equal(GameInputTransition.KeyUp, bridge.Events[^1].Transition);
    }

    private GameBridgePlaybackSession Session(IGameInputBridge bridge, GameBridgeArmController arm) => new(bridge, _mapper, new KeybindProfileValidator(), new Focus(true), arm);
    private GameBridgePlaybackOutput Output(IGameInputBridge bridge) => new(bridge, _mapper, _profile, new Focus(true), "Warframe", true, FastTiming());
    private static GameBridgeTimingOptions FastTiming() => new(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.Zero);
    private static ShawzinEvent Event(decimal seconds, ShawzinFret frets, params ShawzinString[] strings) => new(Guid.NewGuid(), new(seconds), new(strings.Select(x => new ShawzinNote(x, frets)).ToArray()));
    private sealed class Focus(bool focused) : IGameTargetFocusService { public TargetFocusStatus GetStatus(string targetWindowTitle) => new(true, focused, focused ? "Focused" : "Target focus was lost."); }
    private sealed class ImmediateScheduler : IShawzinPlaybackScheduler
    { public long GetTimestamp() => 1; public AbsoluteTime GetElapsedTime(long startingTimestamp) => AbsoluteTime.Zero; public ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; } }
    private sealed class CapturingScheduler : IShawzinPlaybackScheduler
    {
        public List<AbsoluteTime> Targets { get; } = [];
        public long GetTimestamp() => 1;
        public AbsoluteTime GetElapsedTime(long startingTimestamp) => AbsoluteTime.Zero;
        public ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Targets.Add(targetOffset); return ValueTask.CompletedTask; }
    }

    private sealed class FailingBridge : IGameInputBridge
    {
        private int _presses; public bool ReleaseAllCalled { get; private set; }
        public GameInputCapability Capability => new(true, "Fake", "Fake");
        public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) { if (++_presses == 2) throw new InvalidOperationException("Failure"); return ValueTask.CompletedTask; }
        public async ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default) { foreach (var key in keys) await PressKeyAsync(key, eventId, cancellationToken); }
        public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default) { ReleaseAllCalled = true; return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class ThrowOnInputBridge : IGameInputBridge
    {
        public GameInputCapability Capability => new(true, "Guard", "Throws if used");
        private static ValueTask Fail() => ValueTask.FromException(new InvalidOperationException("Real bridge was used."));
        public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default) => Fail();
        public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private static MemoryStream SingleNoteMidi() => new([
        0x4D,0x54,0x68,0x64, 0,0,0,6, 0,0, 0,1, 1,0xE0,
        0x4D,0x54,0x72,0x6B, 0,0,0,0x15, 0,0xFF,3,4,0x4C,0x65,0x61,0x64,
        0,0x90,0x3C,0x64, 0x83,0x60,0x80,0x3C,0, 0,0xFF,0x2F,0]);
}
