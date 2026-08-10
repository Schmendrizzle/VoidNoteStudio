using VoidNote.Domain.Audio;

namespace VoidNote.Audio.Decoding;

/// <summary>Selects the built-in WAV stream decoder or optional FFmpeg adapter by validated extension.</summary>
public sealed class PlatformAudioDecoder(WaveAudioDecoder wave, FfmpegAudioDecoder ffmpeg) : IAudioDecoder
{
    public string BackendId => $"{wave.BackendId}+{ffmpeg.BackendId}";
    public bool IsAvailable => true;
    public Task<AudioFormatInfo> ProbeAsync(string path, CancellationToken cancellationToken = default) => Select(path).ProbeAsync(path, cancellationToken);
    public Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Select(request.Path).DecodeAsync(request, consumer, progress, cancellationToken);
    private IAudioDecoder Select(string path) => Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".wave", StringComparison.OrdinalIgnoreCase) ? wave : ffmpeg;
}
