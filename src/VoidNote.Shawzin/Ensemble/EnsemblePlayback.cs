using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Playback;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>Receives ensemble events while retaining their source-track identity.</summary>
public interface IEnsemblePlaybackOutput
{
    ValueTask PlayAsync(Guid trackId, ShawzinEvent shawzinEvent, CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
    ValueTask PositionChangedAsync(AbsoluteTime position, CancellationToken cancellationToken);
}

/// <summary>Single-anchor transport for all active ensemble tracks.</summary>
public sealed class ShawzinEnsemblePlaybackEngine : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IShawzinPlaybackScheduler _scheduler;
    private readonly IEnsemblePlaybackOutput _output;
    private ShawzinEnsemble? _ensemble;
    private CancellationTokenSource? _cancellation;
    private Task _run = Task.CompletedTask;
    private AbsoluteTime _position = AbsoluteTime.Zero;
    private AbsoluteTime _anchorPosition = AbsoluteTime.Zero;
    private long _anchor;
    private ShawzinPlaybackState _state;

    public ShawzinEnsemblePlaybackEngine(IShawzinPlaybackScheduler scheduler, IEnsemblePlaybackOutput output)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public ShawzinPlaybackState State { get { lock (_gate) return _state; } }
    public AbsoluteTime Position { get { lock (_gate) return _state == ShawzinPlaybackState.Playing ? Add(_anchorPosition, _scheduler.GetElapsedTime(_anchor)) : _position; } }

    public async Task LoadAsync(ShawzinEnsemble ensemble, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ensemble);
        await StopAsync(cancellationToken).ConfigureAwait(false);
        if (ensemble.Tracks.SelectMany(value => value.ShawzinTrack?.ShawzinEvents ?? []).Any(value => value.Position.Seconds < 0m))
            throw new ArgumentException("Ensemble playback positions cannot be negative.", nameof(ensemble));
        lock (_gate) _ensemble = ensemble;
    }

    public Task PlayAsync(Guid? onlyTrackId = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_state == ShawzinPlaybackState.Playing) return _run;
            if (_ensemble is null) throw new InvalidOperationException("Load an ensemble before playback.");
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _anchorPosition = _position;
            _anchor = _scheduler.GetTimestamp();
            _state = ShawzinPlaybackState.Playing;
            _run = RunAsync(onlyTrackId, _cancellation.Token);
            return _run;
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        Task run;
        lock (_gate)
        {
            if (_state != ShawzinPlaybackState.Playing) return;
            _position = Add(_anchorPosition, _scheduler.GetElapsedTime(_anchor));
            _state = ShawzinPlaybackState.Paused;
            _cancellation?.Cancel();
            run = _run;
        }
        await AwaitCancellation(run).ConfigureAwait(false);
        await _output.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SeekAsync(AbsoluteTime position, CancellationToken cancellationToken = default)
    {
        bool resume;
        Task run;
        lock (_gate)
        {
            resume = _state == ShawzinPlaybackState.Playing;
            _cancellation?.Cancel();
            run = _run;
            _position = position;
            _anchorPosition = position;
            _state = resume ? ShawzinPlaybackState.Paused : _state;
        }
        await AwaitCancellation(run).ConfigureAwait(false);
        await _output.StopAsync(cancellationToken).ConfigureAwait(false);
        await _output.PositionChangedAsync(position, cancellationToken).ConfigureAwait(false);
        if (resume) _ = PlayAsync(cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task run;
        lock (_gate)
        {
            _cancellation?.Cancel();
            run = _run;
            _position = AbsoluteTime.Zero;
            _anchorPosition = AbsoluteTime.Zero;
            _state = ShawzinPlaybackState.Stopped;
        }
        await AwaitCancellation(run).ConfigureAwait(false);
        await _output.StopAsync(cancellationToken).ConfigureAwait(false);
        await _output.PositionChangedAsync(AbsoluteTime.Zero, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(Guid? onlyTrackId, CancellationToken cancellationToken)
    {
        long anchor;
        AbsoluteTime start;
        ShawzinEnsemble ensemble;
        lock (_gate) { anchor = _anchor; start = _anchorPosition; ensemble = _ensemble!; }
        try
        {
            var due = ensemble.Tracks.Where(track => onlyTrackId is null || track.Id == onlyTrackId)
                .SelectMany(track => (track.ShawzinTrack?.ShawzinEvents ?? []).Select(value => new DueEvent(track, value)))
                .Where(value => value.Event.Position.Seconds >= start.Seconds)
                .OrderBy(value => value.Event.Position.Seconds).ThenBy(value => value.Track.Id).ThenBy(value => value.Event.Id).ToArray();
            foreach (var item in due)
            {
                await _scheduler.WaitUntilAsync(anchor, new AbsoluteTime(item.Event.Position.Seconds - start.Seconds), cancellationToken).ConfigureAwait(false);
                if (!IsAudible(ensemble, item.Track, onlyTrackId)) continue;
                await _output.PlayAsync(item.Track.Id, item.Event, cancellationToken).ConfigureAwait(false);
                await _output.PositionChangedAsync(item.Event.Position, cancellationToken).ConfigureAwait(false);
            }
            await _output.StopAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (_state == ShawzinPlaybackState.Playing && _anchor == anchor)
                {
                    _position = due.Length == 0 ? start : due[^1].Event.Position;
                    _state = ShawzinPlaybackState.Stopped;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            try { await _output.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            lock (_gate) { _position = AbsoluteTime.Zero; _state = ShawzinPlaybackState.Stopped; }
            throw;
        }
    }

    private static bool IsAudible(ShawzinEnsemble ensemble, ShawzinEnsembleTrack track, Guid? onlyTrackId)
    {
        if (!track.IsActive || track.IsMuted) return false;
        if (onlyTrackId is not null) return track.Id == onlyTrackId;
        var solo = ensemble.Tracks.Any(value => value.IsActive && value.IsSolo && !value.IsMuted);
        return !solo || track.IsSolo;
    }

    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); _cancellation?.Dispose(); }
    private static AbsoluteTime Add(AbsoluteTime left, AbsoluteTime right) => new(left.Seconds + right.Seconds);
    private static async Task AwaitCancellation(Task task) { try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { } catch { } }
    private sealed record DueEvent(ShawzinEnsembleTrack Track, ShawzinEvent Event);
}
