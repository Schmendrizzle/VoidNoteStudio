using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Diagnostic;
using VoidNote.GameBridge.Mapping;
using VoidNote.GameBridge.Profiles;
using VoidNote.GameBridge.Safety;
using VoidNote.Shawzin.Playback;
using VoidNote.Domain.Music;

namespace VoidNote.GameBridge.Playback;

public sealed record DryRunResult(int EventCount, int InputCount, IReadOnlyList<string> MappingErrors, PlaybackDiagnostics Diagnostics, IReadOnlyList<DiagnosticInputEvent> InputLog);

/// <summary>Owns one armed playback lifetime and guarantees release/disarm on all exits.</summary>
public sealed class GameBridgePlaybackSession : IAsyncDisposable
{
    private readonly IGameInputBridge _bridge;
    private readonly IShawzinInputMapper _mapper;
    private readonly IKeybindProfileValidator _validator;
    private readonly IGameTargetFocusService _focus;
    private readonly GameBridgeArmController _arm;
    private readonly IGameBridgeStartDelay _startDelay;
    private readonly object _playbackGate = new();
    private ShawzinPlaybackEngine? _engine;
    private GameBridgePlaybackOutput? _output;
    private CancellationTokenSource? _playbackCancellation;

    public GameBridgePlaybackSession(IGameInputBridge bridge, IShawzinInputMapper mapper, IKeybindProfileValidator validator,
        IGameTargetFocusService focus, GameBridgeArmController arm, IGameBridgeStartDelay startDelay)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _arm = arm ?? throw new ArgumentNullException(nameof(arm));
        _startDelay = startDelay ?? throw new ArgumentNullException(nameof(startDelay));
    }

    public GameInputCapability Capability => _bridge.Capability;
    public GameBridgeArmState ArmState => _arm.State;
    public PlaybackDiagnostics? LastDiagnostics => _output?.Diagnostics;
    public void Arm(bool disclaimerAcknowledged) { if (!_bridge.Capability.IsAvailable) throw new InvalidOperationException(_bridge.Capability.Description); _arm.Arm(disclaimerAcknowledged); }

    public async Task PlayAsync(ShawzinTrack track, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing, string targetTitle, bool requireFocus, CancellationToken token = default)
        => await PlayAsync(track, profile, timing, targetTitle, requireFocus, TimeSpan.Zero, null, token).ConfigureAwait(false);

    public async Task PlayAsync(ShawzinTrack track, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing,
        string targetTitle, bool requireFocus, TimeSpan delay, IProgress<GameBridgeStartProgress>? progress,
        CancellationToken token = default)
    {
        _arm.EnsureArmed(); Validate(profile);
        await StopEngineAsync().ConfigureAwait(false);
        using var playback = BeginPlayback(token);
        try
        {
            if (!await PrepareStartAsync(delay, targetTitle, requireFocus, progress, playback.Token, token).ConfigureAwait(false)) return;
            _output = new(_bridge, _mapper, profile, _focus, targetTitle, requireFocus, timing); _output.Begin();
            _engine = new(new SystemShawzinPlaybackScheduler(), _output);
            await _engine.LoadAsync(track, playback.Token).ConfigureAwait(false);
            await _engine.PlayAsync(playback.Token).ConfigureAwait(false);
        }
        catch { await FailSafeAsync().ConfigureAwait(false); throw; }
        finally { EndPlayback(playback); _arm.Disarm(); }
    }

    public async Task PlayRangeAsync(ShawzinTrack track, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing,
        string targetTitle, bool requireFocus, AbsoluteTime sourceStart, AbsoluteTime duration, CancellationToken token = default)
    {
        _arm.EnsureArmed(); Validate(profile); await StopEngineAsync().ConfigureAwait(false);
        using var range = BeginPlayback(token);
        _output = new(_bridge, _mapper, profile, _focus, targetTitle, requireFocus, timing); _output.Begin();
        _engine = new(new SystemShawzinPlaybackScheduler(), _output);
        if (duration.Seconds > 0) range.CancelAfter(TimeSpan.FromSeconds((double)duration.Seconds));
        try
        {
            await _engine.LoadAsync(track, range.Token).ConfigureAwait(false);
            if (sourceStart.Seconds > 0) await _engine.SeekAsync(sourceStart, range.Token).ConfigureAwait(false);
            await _engine.PlayAsync(range.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (range.IsCancellationRequested && !token.IsCancellationRequested) { }
        catch { await FailSafeAsync().ConfigureAwait(false); throw; }
        finally { EndPlayback(range); _arm.Disarm(); }
    }

    public async Task<DryRunResult> DryRunAsync(ShawzinTrack track, ShawzinKeybindProfile profile, GameBridgeTimingOptions? timing = null, CancellationToken token = default)
    {
        var validation = _validator.Validate(profile);
        var errors = validation.Issues.Select(x => x.Message).ToArray();
        if (errors.Length > 0) return new(track.ShawzinEvents.Count, 0, errors, new([], 0, 0), []);
        await using var diagnostic = new DiagnosticGameInputBridge();
        var output = new GameBridgePlaybackOutput(diagnostic, _mapper, profile, new AlwaysFocusedService(), string.Empty, false, timing ?? GameBridgeTimingOptions.SafeDefault);
        output.Begin();
        await using var engine = new ShawzinPlaybackEngine(new SystemShawzinPlaybackScheduler(), output);
        await engine.LoadAsync(track, token).ConfigureAwait(false); await engine.PlayAsync(token).ConfigureAwait(false);
        return new(track.ShawzinEvents.Count, output.Diagnostics.InputCount, [], output.Diagnostics, diagnostic.Events);
    }

    public async Task PlayDynamicAsync(DynamicShawzinScalePlan plan, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing,
        string targetTitle, bool requireFocus, CancellationToken token = default)
        => await PlayDynamicAsync(plan, profile, timing, targetTitle, requireFocus, TimeSpan.Zero, null, token).ConfigureAwait(false);

    public async Task PlayDynamicAsync(DynamicShawzinScalePlan plan, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing,
        string targetTitle, bool requireFocus, TimeSpan delay, IProgress<GameBridgeStartProgress>? progress,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _arm.EnsureArmed(); Validate(profile); await StopEngineAsync().ConfigureAwait(false);
        using var playback = BeginPlayback(token);
        try
        {
            if (!await PrepareStartAsync(delay, targetTitle, requireFocus, progress, playback.Token, token).ConfigureAwait(false)) return;
            _output = new(_bridge, _mapper, profile, _focus, targetTitle, requireFocus, timing); _output.Begin();
            var engine = new DynamicShawzinPlaybackEngine(new SystemShawzinPlaybackScheduler(), _output);
            await engine.PlayAsync(plan, playback.Token).ConfigureAwait(false);
        }
        catch { await FailSafeAsync().ConfigureAwait(false); throw; }
        finally { EndPlayback(playback); _arm.Disarm(); }
    }

    public async Task<DryRunResult> DryRunDynamicAsync(DynamicShawzinScalePlan plan, ShawzinKeybindProfile profile,
        GameBridgeTimingOptions? timing = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var validation = _validator.Validate(profile);
        var errors = validation.Issues.Select(value => value.Message).ToArray();
        var eventCount = plan.NoteEvents.Count + plan.ScaleChangeEvents.Count;
        if (errors.Length > 0) return new(eventCount, 0, errors, new([], 0, 0), []);
        await using var diagnostic = new DiagnosticGameInputBridge();
        var output = new GameBridgePlaybackOutput(diagnostic, _mapper, profile, new AlwaysFocusedService(), string.Empty, false,
            timing ?? GameBridgeTimingOptions.SafeDefault); output.Begin();
        var engine = new DynamicShawzinPlaybackEngine(new SystemShawzinPlaybackScheduler(), output);
        await engine.PlayAsync(plan, token).ConfigureAwait(false);
        return new(eventCount, output.Diagnostics.InputCount, [], output.Diagnostics, diagnostic.Events);
    }

    public async Task StopAsync()
    {
        lock (_playbackGate) _playbackCancellation?.Cancel();
        await StopEngineAsync().ConfigureAwait(false);
        await _bridge.ReleaseAllAsync(CancellationToken.None).ConfigureAwait(false);
        _arm.Disarm();
    }
    public async Task EmergencyStopAsync() { _output?.RecordEmergencyStop(); await StopAsync().ConfigureAwait(false); }
    private void Validate(ShawzinKeybindProfile profile) { var r = _validator.Validate(profile); if (!r.IsValid) throw new InvalidDataException(string.Join(" ", r.Issues.Select(x => x.Message))); }
    private CancellationTokenSource BeginPlayback(CancellationToken token)
    {
        var playback = CancellationTokenSource.CreateLinkedTokenSource(token);
        lock (_playbackGate)
        {
            if (_playbackCancellation is not null)
            {
                playback.Dispose();
                throw new InvalidOperationException("GameBridge playback is already active.");
            }
            _playbackCancellation = playback;
            _output = null;
        }
        return playback;
    }
    private void EndPlayback(CancellationTokenSource playback)
    {
        lock (_playbackGate)
        {
            if (ReferenceEquals(_playbackCancellation, playback)) _playbackCancellation = null;
        }
    }
    private async Task<bool> PrepareStartAsync(TimeSpan delay, string targetTitle, bool requireFocus,
        IProgress<GameBridgeStartProgress>? progress, CancellationToken playbackToken, CancellationToken callerToken)
    {
        try
        {
            await _startDelay.WaitAsync(delay, progress, playbackToken).ConfigureAwait(false);
            playbackToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (playbackToken.IsCancellationRequested && !callerToken.IsCancellationRequested)
        {
            return false;
        }

        progress?.Report(new(GameBridgeStartPhase.CheckingFocus, delay, TimeSpan.Zero, 1d));
        EnsureInitialFocus(targetTitle, requireFocus);
        playbackToken.ThrowIfCancellationRequested();
        progress?.Report(new(GameBridgeStartPhase.Ready, delay, TimeSpan.Zero, 1d));
        return true;
    }
    private void EnsureInitialFocus(string targetTitle, bool requireFocus)
    {
        if (!requireFocus) return;
        var focus = _focus.GetStatus(targetTitle);
        if (focus.IsSupported && focus.IsTargetFocused) return;
        throw new GameBridgeFocusException(focus.Description);
    }
    private async Task StopEngineAsync() { if (_engine is null) return; await _engine.StopAsync().ConfigureAwait(false); await _engine.DisposeAsync().ConfigureAwait(false); _engine = null; }
    private async Task FailSafeAsync() { try { await _bridge.ReleaseAllAsync(CancellationToken.None).ConfigureAwait(false); } finally { _arm.Disarm(); } }
    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); await _bridge.DisposeAsync().ConfigureAwait(false); }
    private sealed class AlwaysFocusedService : IGameTargetFocusService { public TargetFocusStatus GetStatus(string targetWindowTitle) => new(true, true, "Diagnostic target."); }
}
