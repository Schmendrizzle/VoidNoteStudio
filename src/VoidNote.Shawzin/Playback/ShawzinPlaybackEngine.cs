using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Playback;

/// <summary>UI-independent Shawzin transport using absolute targets from one scheduler anchor.</summary>
public sealed class ShawzinPlaybackEngine : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IShawzinPlaybackScheduler _scheduler;
    private readonly IShawzinPlaybackOutput _output;
    private IReadOnlyList<ShawzinEvent> _events = [];
    private CancellationTokenSource? _cancellation;
    private Task _run = Task.CompletedTask;
    private AbsoluteTime _position = AbsoluteTime.Zero;
    private AbsoluteTime _anchorPosition = AbsoluteTime.Zero;
    private long _anchor;
    private ShawzinPlaybackState _state;

    public ShawzinPlaybackEngine(IShawzinPlaybackScheduler scheduler, IShawzinPlaybackOutput output)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public ShawzinPlaybackState State { get { lock (_gate) return _state; } }
    public AbsoluteTime Position { get { lock (_gate) return _state == ShawzinPlaybackState.Playing ? Add(_anchorPosition, _scheduler.GetElapsedTime(_anchor)) : _position; } }

    public async Task LoadAsync(ShawzinTrack track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        await StopAsync(cancellationToken).ConfigureAwait(false);
        if (track.ShawzinEvents.Zip(track.ShawzinEvents.Skip(1)).Any(pair => pair.First.Position.Seconds >= pair.Second.Position.Seconds))
            throw new ArgumentException("Playback events must be strictly ordered.", nameof(track));
        lock (_gate) _events = track.ShawzinEvents.ToArray();
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_state == ShawzinPlaybackState.Playing) return _run;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _anchorPosition = _position;
            _anchor = _scheduler.GetTimestamp();
            _state = ShawzinPlaybackState.Playing;
            _run = RunAsync(_cancellation.Token);
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
        if (resume) _ = PlayAsync(cancellationToken);
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cancellation?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            ShawzinEvent[] due;
            AbsoluteTime start;
            long anchor;
            lock (_gate)
            {
                start = _anchorPosition;
                anchor = _anchor;
                due = _events.Where(value => value.Position.Seconds >= start.Seconds).ToArray();
            }
            foreach (var shawzinEvent in due)
            {
                var lead = (_output as IShawzinPlaybackTimingOutput)?.KeyDownLead.Seconds ?? 0m;
                var dispatchOffset = Math.Max(0m, shawzinEvent.Position.Seconds - start.Seconds - lead);
                await _scheduler.WaitUntilAsync(anchor, new AbsoluteTime(dispatchOffset), cancellationToken).ConfigureAwait(false);
                if (shawzinEvent.Chord.Notes.Count == 1) await _output.PlayNoteAsync(shawzinEvent, cancellationToken).ConfigureAwait(false);
                else await _output.PlayChordAsync(shawzinEvent, cancellationToken).ConfigureAwait(false);
                await _output.PositionChangedAsync(shawzinEvent.Position, cancellationToken).ConfigureAwait(false);
            }
            await _output.StopAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (_state == ShawzinPlaybackState.Playing && _anchor == anchor)
                {
                    _position = due.Length == 0 ? start : due[^1].Position;
                    _state = ShawzinPlaybackState.Stopped;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            try { await _output.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            lock (_gate)
            {
                _position = AbsoluteTime.Zero;
                _anchorPosition = AbsoluteTime.Zero;
                _state = ShawzinPlaybackState.Stopped;
            }
            throw;
        }
    }

    private static AbsoluteTime Add(AbsoluteTime left, AbsoluteTime right) => new(left.Seconds + right.Seconds);
    private static async Task AwaitCancellation(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch { /* The original PlayAsync task remains the authoritative error channel; cleanup is idempotent. */ }
    }
}
