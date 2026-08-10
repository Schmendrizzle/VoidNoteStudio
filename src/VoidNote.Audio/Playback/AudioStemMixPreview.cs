using Microsoft.Extensions.Logging;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Playback;

/// <summary>Coordinates several existing preview engines against one monotonic clock for a lightweight stem mix.</summary>
public sealed class AudioStemMixPreview(
    IAudioDecoder decoder,
    IAudioPlaybackClock clock,
    IAudioDeviceProvider devices,
    ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private readonly List<AudioPlaybackEngine> _engines = [];
    private CancellationTokenSource? _run;

    public async Task PlayAsync(VoidNoteProject project, IReadOnlyList<AudioTrack> tracks, CancellationToken cancellationToken = default)
    {
        await StopAsync();
        var audible = tracks.Where(track => track.IsEnabled && !track.IsMuted && (!tracks.Any(value => value.IsSolo) || track.IsSolo)).ToArray();
        if (audible.Length == 0) throw new InvalidOperationException("Select at least one audible stem track.");
        _run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        foreach (var track in audible)
        {
            var engine = new AudioPlaybackEngine(decoder, clock, loggerFactory.CreateLogger<AudioPlaybackEngine>());
            var device = await devices.OpenDefaultAsync(_run.Token); await engine.LoadAsync(project, track, device, cancellationToken: _run.Token); _engines.Add(engine);
        }
        var starts = _engines.Select(engine => engine.PlayAsync(_run.Token)).ToArray();
        await Task.WhenAll(starts);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _run?.Cancel();
        foreach (var engine in _engines) { await engine.StopAsync(cancellationToken); await engine.DisposeAsync(); }
        _engines.Clear(); _run?.Dispose(); _run = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
