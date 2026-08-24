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
    private ShawzinPlaybackEngine? _engine;
    private GameBridgePlaybackOutput? _output;
    private CancellationTokenSource? _dynamicCancellation;

    public GameBridgePlaybackSession(IGameInputBridge bridge, IShawzinInputMapper mapper, IKeybindProfileValidator validator, IGameTargetFocusService focus, GameBridgeArmController arm)
    { _bridge = bridge; _mapper = mapper; _validator = validator; _focus = focus; _arm = arm; }

    public GameInputCapability Capability => _bridge.Capability;
    public GameBridgeArmState ArmState => _arm.State;
    public PlaybackDiagnostics? LastDiagnostics => _output?.Diagnostics;
    public void Arm(bool disclaimerAcknowledged) { if (!_bridge.Capability.IsAvailable) throw new InvalidOperationException(_bridge.Capability.Description); _arm.Arm(disclaimerAcknowledged); }

    public async Task PlayAsync(ShawzinTrack track, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing, string targetTitle, bool requireFocus, CancellationToken token = default)
    {
        _arm.EnsureArmed(); Validate(profile);
        await StopEngineAsync().ConfigureAwait(false);
        _output = new(_bridge, _mapper, profile, _focus, targetTitle, requireFocus, timing); _output.Begin();
        _engine = new(new SystemShawzinPlaybackScheduler(), _output);
        try { await _engine.LoadAsync(track, token).ConfigureAwait(false); await _engine.PlayAsync(token).ConfigureAwait(false); }
        catch { await FailSafeAsync().ConfigureAwait(false); throw; }
        finally { _arm.Disarm(); }
    }

    public async Task PlayRangeAsync(ShawzinTrack track, ShawzinKeybindProfile profile, GameBridgeTimingOptions timing,
        string targetTitle, bool requireFocus, AbsoluteTime sourceStart, AbsoluteTime duration, CancellationToken token = default)
    {
        _arm.EnsureArmed(); Validate(profile); await StopEngineAsync().ConfigureAwait(false);
        _output = new(_bridge, _mapper, profile, _focus, targetTitle, requireFocus, timing); _output.Begin();
        _engine = new(new SystemShawzinPlaybackScheduler(), _output);
        using var range = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (duration.Seconds > 0) range.CancelAfter(TimeSpan.FromSeconds((double)duration.Seconds));
        try
        {
            await _engine.LoadAsync(track, range.Token).ConfigureAwait(false);
            if (sourceStart.Seconds > 0) await _engine.SeekAsync(sourceStart, range.Token).ConfigureAwait(false);
            await _engine.PlayAsync(range.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (range.IsCancellationRequested && !token.IsCancellationRequested) { }
        catch { await FailSafeAsync().ConfigureAwait(false); throw; }
        finally { _arm.Disarm(); }
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
    {
        ArgumentNullException.ThrowIfNull(plan);
        _arm.EnsureArmed(); Validate(profile); await StopEngineAsync().ConfigureAwait(false);
        _dynamicCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _output = new(_bridge, _mapper, profile, _focus, targetTitle, requireFocus, timing); _output.Begin();
        var engine = new DynamicShawzinPlaybackEngine(new SystemShawzinPlaybackScheduler(), _output);
        try { await engine.PlayAsync(plan, _dynamicCancellation.Token).ConfigureAwait(false); }
        catch { await FailSafeAsync().ConfigureAwait(false); throw; }
        finally { _dynamicCancellation.Dispose(); _dynamicCancellation = null; _arm.Disarm(); }
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

    public async Task StopAsync() { _dynamicCancellation?.Cancel(); await StopEngineAsync().ConfigureAwait(false); await _bridge.ReleaseAllAsync(CancellationToken.None).ConfigureAwait(false); _arm.Disarm(); }
    public async Task EmergencyStopAsync() { _output?.RecordEmergencyStop(); await StopAsync().ConfigureAwait(false); }
    private void Validate(ShawzinKeybindProfile profile) { var r = _validator.Validate(profile); if (!r.IsValid) throw new InvalidDataException(string.Join(" ", r.Issues.Select(x => x.Message))); }
    private async Task StopEngineAsync() { if (_engine is null) return; await _engine.StopAsync().ConfigureAwait(false); await _engine.DisposeAsync().ConfigureAwait(false); _engine = null; }
    private async Task FailSafeAsync() { try { await _bridge.ReleaseAllAsync(CancellationToken.None).ConfigureAwait(false); } finally { _arm.Disarm(); } }
    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); await _bridge.DisposeAsync().ConfigureAwait(false); }
    private sealed class AlwaysFocusedService : IGameTargetFocusService { public TargetFocusStatus GetStatus(string targetWindowTitle) => new(true, true, "Diagnostic target."); }
}
