using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Midi.Playback;

/// <summary>Identifies the transport state of the MIDI playback core.</summary>
public enum MidiPlaybackState
{
    /// <summary>Playback is at rest.</summary>
    Stopped,
    /// <summary>Playback is actively scheduling events.</summary>
    Playing,
    /// <summary>Playback retains its current position.</summary>
    Paused,
}

/// <summary>UI-independent MIDI transport based on the VoidNote master timeline.</summary>
public sealed class MidiPlaybackEngine : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IPlaybackScheduler _scheduler;
    private readonly IMidiPlaybackOutput _output;
    private IReadOnlyList<ScheduledMidiEvent> _events = [];
    private CancellationTokenSource? _runCancellation;
    private Task _runTask = Task.CompletedTask;
    private AbsoluteTime _position = AbsoluteTime.Zero;
    private AbsoluteTime _anchorPosition = AbsoluteTime.Zero;
    private long _anchorTimestamp;
    private MidiPlaybackState _state;

    /// <summary>Creates a playback engine with replaceable scheduling and output.</summary>
    public MidiPlaybackEngine(IPlaybackScheduler scheduler, IMidiPlaybackOutput output)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>Gets the current transport state.</summary>
    public MidiPlaybackState State { get { lock (_gate) return _state; } }

    /// <summary>Gets the current absolute song position.</summary>
    public AbsoluteTime Position
    {
        get
        {
            lock (_gate)
            {
                return _state == MidiPlaybackState.Playing
                    ? Add(_anchorPosition, _scheduler.GetElapsedTime(_anchorTimestamp))
                    : _position;
            }
        }
    }

    /// <summary>Loads normalized tracks and resets the transport.</summary>
    public async Task LoadAsync(ProjectTimeline timeline, IReadOnlyList<MidiTrack> tracks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(tracks);
        await StopAsync(cancellationToken).ConfigureAwait(false);
        var events = tracks
            .SelectMany(track => track.Events.SelectMany(note => CreateEvents(timeline, track.Id, note)))
            .OrderBy(midiEvent => midiEvent, ScheduledEventComparer.Instance)
            .ToArray();
        lock (_gate) _events = events;
    }

    /// <summary>Starts or resumes playback and returns a task that completes with the current run.</summary>
    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_state == MidiPlaybackState.Playing) return _runTask;
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _anchorPosition = _position;
            _anchorTimestamp = _scheduler.GetTimestamp();
            _state = MidiPlaybackState.Playing;
            _runTask = RunAsync(_runCancellation.Token);
            return _runTask;
        }
    }

    /// <summary>Pauses playback while retaining the current position.</summary>
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        Task task;
        lock (_gate)
        {
            if (_state != MidiPlaybackState.Playing) return;
            _position = Add(_anchorPosition, _scheduler.GetElapsedTime(_anchorTimestamp));
            _state = MidiPlaybackState.Paused;
            _runCancellation?.Cancel();
            task = _runTask;
        }

        await AwaitCancellationAsync(task).ConfigureAwait(false);
        await _output.AllNotesOffAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops playback and returns to the beginning.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task task;
        lock (_gate)
        {
            _runCancellation?.Cancel();
            task = _runTask;
            _position = AbsoluteTime.Zero;
            _anchorPosition = AbsoluteTime.Zero;
            _state = MidiPlaybackState.Stopped;
        }

        await AwaitCancellationAsync(task).ConfigureAwait(false);
        await _output.AllNotesOffAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves the transport to an absolute position and resumes if it was playing.</summary>
    public async Task SeekAsync(AbsoluteTime position, CancellationToken cancellationToken = default)
    {
        bool resume;
        Task task;
        lock (_gate)
        {
            resume = _state == MidiPlaybackState.Playing;
            _runCancellation?.Cancel();
            task = _runTask;
            _position = position;
            _anchorPosition = position;
            _state = resume ? MidiPlaybackState.Paused : _state;
        }

        await AwaitCancellationAsync(task).ConfigureAwait(false);
        await _output.AllNotesOffAsync(cancellationToken).ConfigureAwait(false);
        if (resume) _ = PlayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _runCancellation?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            ScheduledMidiEvent[] dueEvents;
            AbsoluteTime startPosition;
            long startTimestamp;
            lock (_gate)
            {
                startPosition = _anchorPosition;
                startTimestamp = _anchorTimestamp;
                dueEvents = _events.Where(item => item.Time.Seconds >= startPosition.Seconds).ToArray();
            }

            foreach (var midiEvent in dueEvents)
            {
                var offset = new AbsoluteTime(midiEvent.Time.Seconds - startPosition.Seconds);
                await _scheduler.WaitUntilAsync(startTimestamp, offset, cancellationToken).ConfigureAwait(false);
                await _output.SendAsync(midiEvent, cancellationToken).ConfigureAwait(false);
            }

            lock (_gate)
            {
                if (_state == MidiPlaybackState.Playing && _anchorTimestamp == startTimestamp)
                {
                    _position = dueEvents.Length == 0 ? startPosition : dueEvents[^1].Time;
                    _state = MidiPlaybackState.Stopped;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_gate)
            {
                if (_state == MidiPlaybackState.Playing)
                {
                    _position = Add(_anchorPosition, _scheduler.GetElapsedTime(_anchorTimestamp));
                    _state = MidiPlaybackState.Stopped;
                }
            }
        }
    }

    private static IEnumerable<ScheduledMidiEvent> CreateEvents(ProjectTimeline timeline, Guid trackId, MusicalEvent note)
    {
        yield return new ScheduledMidiEvent(note.Id, trackId, timeline.ToAbsoluteTime(note.StartTime), ScheduledMidiEventKind.NoteOn, note.Pitch, note.Velocity);
        yield return new ScheduledMidiEvent(note.Id, trackId, timeline.ToAbsoluteTime(note.StartTime + note.Duration), ScheduledMidiEventKind.NoteOff, note.Pitch, 0);
    }

    private static AbsoluteTime Add(AbsoluteTime left, AbsoluteTime right) => new(left.Seconds + right.Seconds);

    private static async Task AwaitCancellationAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private sealed class ScheduledEventComparer : IComparer<ScheduledMidiEvent>
    {
        public static ScheduledEventComparer Instance { get; } = new();

        public int Compare(ScheduledMidiEvent? left, ScheduledMidiEvent? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            var time = left.Time.Seconds.CompareTo(right.Time.Seconds);
            if (time != 0) return time;
            if (left.EventId == right.EventId) return left.Kind.CompareTo(right.Kind);
            var kind = right.Kind.CompareTo(left.Kind);
            return kind != 0 ? kind : left.EventId.CompareTo(right.EventId);
        }
    }
}
