using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;

namespace VoidNote.Audio.Decoding;

public enum AudioDecodeError { FileNotFound, Unreadable, InvalidAudio, UnsupportedCodec, DecoderUnavailable, DecodeFailed }

public sealed class AudioDecoderException(AudioDecodeError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public AudioDecodeError Error { get; } = error;
}

public sealed record AudioDecodeRequest(string Path, AbsoluteTime Start, AbsoluteTime? Duration = null);

public sealed record AudioPcmChunk(float[] Samples, int SampleRate, int ChannelCount)
{
    public int FrameCount => Samples.Length / ChannelCount;
}

public interface IAudioDecoder
{
    string BackendId { get; }
    bool IsAvailable { get; }
    Task<AudioFormatInfo> ProbeAsync(string path, CancellationToken cancellationToken = default);
    Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

public static class SupportedAudioFormats
{
    public static IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { ".wav", ".wave", ".flac", ".mp3" };
}
