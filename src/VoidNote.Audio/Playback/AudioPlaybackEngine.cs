using Microsoft.Extensions.Logging;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Playback;

public enum AudioPlaybackState { Stopped, Playing, Paused }

/// <summary>Streams one project track against the shared timeline using a fixed monotonic anchor.</summary>
public sealed class AudioPlaybackEngine(IAudioDecoder decoder, IAudioPlaybackClock clock, ILogger<AudioPlaybackEngine> logger) : IAsyncDisposable
{
    private readonly object _gate = new(); private CancellationTokenSource? _runCancellation; private Task _run = Task.CompletedTask;
    private VoidNoteProject? _project; private AudioTrack? _track; private IAudioOutputDevice? _device; private AbsoluteTime _position = AbsoluteTime.Zero;
    private AbsoluteTime _anchorPosition = AbsoluteTime.Zero; private long _anchor; private AudioRegion? _region;
    public AudioPlaybackState State { get; private set; }
    public AbsoluteTime Position { get { lock (_gate) return State == AudioPlaybackState.Playing ? Clamp(Add(_anchorPosition, clock.GetElapsedTime(_anchor)), Duration) : _position; } }
    public AbsoluteTime Duration { get; private set; } = AbsoluteTime.Zero;

    public Task LoadAsync(VoidNoteProject project, AudioTrack track, IAudioOutputDevice device, AudioRegion? region = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(track); ArgumentNullException.ThrowIfNull(device);
        if (!project.AudioTracks.Contains(track)) throw new ArgumentException("The audio track is not part of the project.", nameof(track));
        lock (_gate)
        {
            _project = project; _track = track; _device = device; _region = region; _position = region?.Start ?? AbsoluteTime.Zero; State = AudioPlaybackState.Stopped;
            Duration = new AbsoluteTime(track.Clips.Select(clip => project.Timeline.ToAbsoluteTime(clip.Start).Seconds + clip.Duration.Seconds).DefaultIfEmpty(0).Max());
        }
        return Task.CompletedTask;
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            EnsureLoaded(); if (State == AudioPlaybackState.Playing) return _run;
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); _anchorPosition = _position; _anchor = clock.GetTimestamp(); State = AudioPlaybackState.Playing;
            _run = RunAsync(_anchorPosition, _anchor, _runCancellation.Token); return _run;
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        Task run; lock (_gate) { if (State != AudioPlaybackState.Playing) return; _position = Position; State = AudioPlaybackState.Paused; _runCancellation?.Cancel(); run = _run; }
        await ObserveCancellationAsync(run); if (_device is not null) await _device.StopAsync(cancellationToken); logger.LogInformation("Audio playback paused at {Position}", _position.Seconds);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task run; lock (_gate) { _runCancellation?.Cancel(); run = _run; State = AudioPlaybackState.Stopped; _position = _region?.Start ?? AbsoluteTime.Zero; }
        await ObserveCancellationAsync(run); if (_device is not null) await _device.StopAsync(cancellationToken); logger.LogInformation("Audio playback stopped");
    }

    public async Task SeekAsync(AbsoluteTime position, CancellationToken cancellationToken = default)
    {
        bool resume; lock (_gate) { if (position.Seconds > Duration.Seconds) throw new ArgumentOutOfRangeException(nameof(position)); resume = State == AudioPlaybackState.Playing; }
        if (resume) await PauseAsync(cancellationToken); lock (_gate) _position = position; if (resume) _ = PlayAsync(cancellationToken);
    }

    private async Task RunAsync(AbsoluteTime start, long anchor, CancellationToken token)
    {
        var project = _project!; var track = _track!; var device = _device!;
        try
        {
            var audible = track.IsEnabled && !track.IsMuted && (!project.AudioTracks.Any(value => value.IsSolo) || track.IsSolo);
            foreach (var clip in track.Clips.Where(value => value.IsEnabled).OrderBy(value => value.Start.Ticks))
            {
                var clipStart = project.Timeline.ToAbsoluteTime(clip.Start); var clipEnd = new AbsoluteTime(clipStart.Seconds + clip.Duration.Seconds);
                if (_region is not null && _region.End.Seconds > _region.Start.Seconds && clipEnd.Seconds > _region.End.Seconds) clipEnd = _region.End;
                if (clipEnd.Seconds <= start.Seconds) continue;
                if (_region is not null && _region.End.Seconds > _region.Start.Seconds && clipStart.Seconds >= _region.End.Seconds) break;
                var begin = Math.Max(start.Seconds, clipStart.Seconds); await clock.DelayUntilAsync(anchor, TimeSpan.FromSeconds((double)(begin - start.Seconds)), token);
                var source = project.AudioSources.Single(value => value.Id == clip.SourceId); var path = source.ResolvedPath ?? source.SourcePath;
                var sourceOffset = new AbsoluteTime(clip.TrimIn.Seconds + Math.Max(0, begin - clipStart.Seconds)); var remaining = new AbsoluteTime(clipEnd.Seconds - begin);
                var format = source.Format; await device.StartAsync(format.SampleRate, format.ChannelCount, token);
                await decoder.DecodeAsync(new(path, sourceOffset, remaining), async (chunk, cancellation) =>
                {
                    var gain = audible ? (float)(track.Gain * clip.Gain) : 0f; if (gain != 1f) for (var index = 0; index < chunk.Samples.Length; index++) chunk.Samples[index] = Math.Clamp(chunk.Samples[index] * gain, -1, 1);
                    await device.WriteAsync(chunk, cancellation);
                }, cancellationToken: token);
                await device.StopAsync(token);
            }
            lock (_gate) { if (State == AudioPlaybackState.Playing && _anchor == anchor) { _position = _region?.LoopEnabled == true ? _region.Start : AbsoluteTime.Zero; State = AudioPlaybackState.Stopped; } }
            if (_region?.LoopEnabled == true && !token.IsCancellationRequested) _ = PlayAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogError(exception, "Audio playback failed"); lock (_gate) { State = AudioPlaybackState.Stopped; _position = AbsoluteTime.Zero; } throw; }
    }

    private void EnsureLoaded() { if (_project is null || _track is null || _device is null) throw new InvalidOperationException("Load an audio track before playback."); }
    private static AbsoluteTime Add(AbsoluteTime value, TimeSpan elapsed) => new(value.Seconds + (decimal)elapsed.TotalSeconds);
    private static AbsoluteTime Clamp(AbsoluteTime value, AbsoluteTime maximum) => value.Seconds > maximum.Seconds ? maximum : value;
    private static async Task ObserveCancellationAsync(Task task) { try { await task; } catch (OperationCanceledException) { } }
    public async ValueTask DisposeAsync() { await StopAsync(); if (_device is not null) await _device.DisposeAsync(); _runCancellation?.Dispose(); }
}
