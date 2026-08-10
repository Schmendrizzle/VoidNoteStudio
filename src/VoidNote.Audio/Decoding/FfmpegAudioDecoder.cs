using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;

namespace VoidNote.Audio.Decoding;

/// <summary>Optional FFmpeg 8.x process adapter used for compressed formats; no FFmpeg type crosses this boundary.</summary>
public sealed class FfmpegAudioDecoder : IAudioDecoder
{
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private readonly ILogger<FfmpegAudioDecoder> _logger;

    public FfmpegAudioDecoder(ILogger<FfmpegAudioDecoder> logger, string ffmpeg = "ffmpeg", string ffprobe = "ffprobe")
    { _logger = logger; _ffmpeg = ffmpeg; _ffprobe = ffprobe; }

    public string BackendId => "ffmpeg-cli-8.1.2";
    public bool IsAvailable => FindExecutable(_ffmpeg) is not null && FindExecutable(_ffprobe) is not null;

    public async Task<AudioFormatInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureFile(path); EnsureAvailable();
        using var process = Start(_ffprobe,
        ["-v", "error", "-show_entries", "format=duration,format_name,bit_rate:format_tags=title,artist:stream=codec_type,codec_name,sample_rate,channels,bits_per_raw_sample,bits_per_sample", "-of", "json", path]);
        try
        {
            var jsonTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken); var json = await jsonTask; var error = await errorTask;
            if (process.ExitCode != 0) throw Classify(error, "FFprobe could not read the audio file.");
            using var document = JsonDocument.Parse(json); var root = document.RootElement;
            var stream = root.GetProperty("streams").EnumerateArray().FirstOrDefault(value => String(value, "codec_type") == "audio");
            if (stream.ValueKind == JsonValueKind.Undefined) throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "The file contains no audio stream.");
            var format = root.GetProperty("format");
            var sampleRate = Int(stream, "sample_rate"); var channels = Int(stream, "channels");
            if (sampleRate <= 0 || channels <= 0) throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "The audio stream has invalid sample-rate or channel metadata.");
            var duration = Decimal(format, "duration");
            return new AudioFormatInfo
            {
                Container = String(format, "format_name") ?? Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                Codec = String(stream, "codec_name") ?? "unknown", SampleRate = sampleRate, ChannelCount = channels,
                BitDepth = NullableInt(stream, "bits_per_raw_sample") ?? NullableInt(stream, "bits_per_sample"),
                BitRate = NullableLong(format, "bit_rate"), Duration = new AbsoluteTime(duration),
                Channels = Enumerable.Range(0, channels).Select(index => new AudioChannelInfo(index, channels == 1 ? "Mono" : index == 0 ? "Left" : index == 1 ? "Right" : $"Channel {index + 1}")).ToArray(),
                Title = Tag(format, "title"), Artist = Tag(format, "artist"),
            };
        }
        catch (OperationCanceledException) { TryKill(process); throw; }
        catch (JsonException exception) { throw new AudioDecoderException(AudioDecodeError.InvalidAudio, "FFprobe returned invalid metadata.", exception); }
    }

    public async Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureFile(request.Path); EnsureAvailable(); var info = await ProbeAsync(request.Path, cancellationToken);
        var length = request.Duration?.Seconds ?? Math.Max(0, info.Duration.Seconds - request.Start.Seconds);
        var args = new List<string> { "-v", "error", "-nostdin" };
        if (request.Start.Seconds > 0) { args.Add("-ss"); args.Add(request.Start.Seconds.ToString(CultureInfo.InvariantCulture)); }
        args.AddRange(["-i", request.Path]);
        if (request.Duration is not null) { args.Add("-t"); args.Add(length.ToString(CultureInfo.InvariantCulture)); }
        args.AddRange(["-f", "f32le", "-acodec", "pcm_f32le", "-ar", info.SampleRate.ToString(CultureInfo.InvariantCulture), "-ac", info.ChannelCount.ToString(CultureInfo.InvariantCulture), "pipe:1"]);
        using var process = Start(_ffmpeg, args); var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var bytes = new byte[4096 * info.ChannelCount * sizeof(float) + 3]; long frames = 0; var carry = 0;
        try
        {
            while (true)
            {
                var count = await process.StandardOutput.BaseStream.ReadAsync(bytes.AsMemory(carry, bytes.Length - carry), cancellationToken); if (count == 0) break;
                var total = carry + count; var usable = total - total % sizeof(float); var samples = new float[usable / sizeof(float)]; Buffer.BlockCopy(bytes, 0, samples, 0, usable);
                carry = total - usable; if (carry > 0) Buffer.BlockCopy(bytes, usable, bytes, 0, carry);
                if (samples.Length > 0)
                {
                    await consumer(new AudioPcmChunk(samples, info.SampleRate, info.ChannelCount), cancellationToken);
                    frames += samples.Length / info.ChannelCount; progress?.Report(length <= 0 ? 1 : Math.Min(1, frames / (double)info.SampleRate / (double)length));
                }
            }
            if (carry != 0) throw new AudioDecoderException(AudioDecodeError.DecodeFailed, "FFmpeg returned an incomplete PCM sample.");
            await process.WaitForExitAsync(cancellationToken); var error = await errorTask;
            if (process.ExitCode != 0) throw Classify(error, "FFmpeg failed while decoding audio.");
        }
        catch (OperationCanceledException) { TryKill(process); throw; }
        catch (AudioDecoderException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        { TryKill(process); throw new AudioDecoderException(AudioDecodeError.DecodeFailed, "FFmpeg audio decoding failed.", exception); }
    }

    private Process Start(string executable, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        _logger.LogInformation("Starting audio decoder process {Executable}", executable);
        try { return Process.Start(info) ?? throw new InvalidOperationException("The decoder process did not start."); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        { throw new AudioDecoderException(AudioDecodeError.DecoderUnavailable, "FFmpeg/ffprobe is not installed or could not be started.", exception); }
    }

    private void EnsureAvailable() { if (!IsAvailable) throw new AudioDecoderException(AudioDecodeError.DecoderUnavailable, "FFmpeg and ffprobe are required for MP3 and FLAC. Configure or install FFmpeg 8.1.2 or a compatible newer build; WAV remains available."); }
    private static void EnsureFile(string path) { if (!File.Exists(path)) throw new AudioDecoderException(AudioDecodeError.FileNotFound, $"Audio file not found: {path}"); }
    private static AudioDecoderException Classify(string detail, string fallback) => new(detail.Contains("Invalid data", StringComparison.OrdinalIgnoreCase) ? AudioDecodeError.InvalidAudio : detail.Contains("Unknown decoder", StringComparison.OrdinalIgnoreCase) ? AudioDecodeError.UnsupportedCodec : AudioDecodeError.DecodeFailed, string.IsNullOrWhiteSpace(detail) ? fallback : $"{fallback} {detail.Trim()}");
    private static string? FindExecutable(string name) { if (Path.IsPathRooted(name)) return File.Exists(name) ? name : null; var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty; return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator).Select(path => Path.Combine(path, name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name : name + suffix)).FirstOrDefault(File.Exists); }
    private static void TryKill(Process process) { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } }
    private static string? String(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static int Int(JsonElement value, string name) => int.TryParse(String(value, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : value.TryGetProperty(name, out var item) && item.TryGetInt32(out result) ? result : 0;
    private static int? NullableInt(JsonElement value, string name) { var result = Int(value, name); return result > 0 ? result : null; }
    private static long? NullableLong(JsonElement value, string name) => long.TryParse(String(value, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static decimal Decimal(JsonElement value, string name) => decimal.TryParse(String(value, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && result >= 0 ? result : 0;
    private static string? Tag(JsonElement format, string name) => format.TryGetProperty("tags", out var tags) ? String(tags, name) : null;
}
