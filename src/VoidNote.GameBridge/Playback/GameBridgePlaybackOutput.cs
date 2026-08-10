using System.Diagnostics;
using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Mapping;
using VoidNote.GameBridge.Profiles;
using VoidNote.GameBridge.Safety;
using VoidNote.Shawzin.Playback;

namespace VoidNote.GameBridge.Playback;

public sealed record GameBridgeTimingOptions(TimeSpan KeyDownLead, TimeSpan HoldDuration, TimeSpan ReleaseDelay)
{
    public static GameBridgeTimingOptions SafeDefault { get; } = new(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(5));
    public void Validate()
    {
        if (KeyDownLead < TimeSpan.Zero || KeyDownLead > TimeSpan.FromMilliseconds(100)) throw new ArgumentOutOfRangeException(nameof(KeyDownLead));
        if (HoldDuration < TimeSpan.FromMilliseconds(1) || HoldDuration > TimeSpan.FromMilliseconds(250)) throw new ArgumentOutOfRangeException(nameof(HoldDuration));
        if (ReleaseDelay < TimeSpan.Zero || ReleaseDelay > TimeSpan.FromMilliseconds(100)) throw new ArgumentOutOfRangeException(nameof(ReleaseDelay));
    }
}

public sealed record PlaybackDiagnosticEvent(Guid EventId, AbsoluteTime PlannedTime, TimeSpan ActualDispatchTime, TimeSpan TimingDeviation, int InputCount, bool Aborted);
public sealed record PlaybackDiagnostics(IReadOnlyList<PlaybackDiagnosticEvent> Events, int FocusLosses, int EmergencyStops)
{
    public int InputCount => Events.Sum(x => x.InputCount);
    public int AbortedEvents => Events.Count(x => x.Aborted);
}

/// <summary>Adapts the existing Shawzin output port to portable, focus-checked keyboard actions.</summary>
public sealed class GameBridgePlaybackOutput : IShawzinPlaybackOutput, IShawzinPlaybackTimingOutput
{
    private readonly IGameInputBridge _bridge;
    private readonly IShawzinInputMapper _mapper;
    private readonly ShawzinKeybindProfile _profile;
    private readonly IGameTargetFocusService _focus;
    private readonly string _targetTitle;
    private readonly bool _requireFocus;
    private readonly GameBridgeTimingOptions _timing;
    private readonly Stopwatch _clock = new();
    private readonly List<PlaybackDiagnosticEvent> _events = [];
    private int _focusLosses;
    private int _emergencyStops;

    public GameBridgePlaybackOutput(IGameInputBridge bridge, IShawzinInputMapper mapper, ShawzinKeybindProfile profile,
        IGameTargetFocusService focus, string targetTitle, bool requireFocus, GameBridgeTimingOptions timing)
    {
        _bridge = bridge; _mapper = mapper; _profile = profile; _focus = focus;
        _targetTitle = targetTitle; _requireFocus = requireFocus; _timing = timing; timing.Validate();
    }

    public AbsoluteTime KeyDownLead => new((decimal)_timing.KeyDownLead.TotalSeconds);
    public PlaybackDiagnostics Diagnostics => new(_events.ToArray(), _focusLosses, _emergencyStops);
    public void Begin() { _events.Clear(); _focusLosses = 0; _emergencyStops = 0; _clock.Restart(); }
    public void RecordEmergencyStop() => Interlocked.Increment(ref _emergencyStops);
    public ValueTask PlayNoteAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken) => DispatchAsync(shawzinEvent, cancellationToken);
    public ValueTask PlayChordAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken) => DispatchAsync(shawzinEvent, cancellationToken);

    private async ValueTask DispatchAsync(ShawzinEvent value, CancellationToken token)
    {
        var focus = _focus.GetStatus(_targetTitle);
        if (_requireFocus && (!focus.IsSupported || !focus.IsTargetFocused))
        {
            Interlocked.Increment(ref _focusLosses);
            _events.Add(new(value.Id, value.Position, _clock.Elapsed, _clock.Elapsed - TimeSpan.FromSeconds((double)value.Position.Seconds), 0, true));
            throw new GameBridgeFocusException(focus.Description);
        }
        var action = _mapper.Map(value, _profile);
        var actual = _clock.Elapsed;
        try
        {
            await _bridge.PressKeysAsync(action.FretKeys, value.Id, token).ConfigureAwait(false);
            await _bridge.PressKeysAsync(action.StringKeys, value.Id, token).ConfigureAwait(false);
            await Task.Delay(_timing.HoldDuration, token).ConfigureAwait(false);
            foreach (var key in action.StringKeys.Reverse()) await _bridge.ReleaseKeyAsync(key, value.Id, CancellationToken.None).ConfigureAwait(false);
            if (_timing.ReleaseDelay > TimeSpan.Zero) await Task.Delay(_timing.ReleaseDelay, token).ConfigureAwait(false);
            foreach (var key in action.FretKeys.Reverse()) await _bridge.ReleaseKeyAsync(key, value.Id, CancellationToken.None).ConfigureAwait(false);
            _events.Add(new(value.Id, value.Position, actual, actual - TimeSpan.FromSeconds((double)value.Position.Seconds), action.AllKeys.Count * 2, false));
        }
        catch
        {
            await _bridge.ReleaseAllAsync(CancellationToken.None).ConfigureAwait(false);
            _events.Add(new(value.Id, value.Position, actual, actual - TimeSpan.FromSeconds((double)value.Position.Seconds), 0, true));
            throw;
        }
    }

    public ValueTask PositionChangedAsync(AbsoluteTime position, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public ValueTask StopAsync(CancellationToken cancellationToken) => _bridge.ReleaseAllAsync(CancellationToken.None);
}
