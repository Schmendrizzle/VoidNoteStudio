using System.Text;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;

namespace VoidNote.Audio.Tests;

internal sealed class AudioFixtureDirectory : IDisposable
{
    public AudioFixtureDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VoidNoteAudioTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
    public string Path { get; }
    public string CreateWave(string name, int sampleRate = 8000, int channels = 1, double seconds = 0.1, Func<int, float>? sample = null)
    {
        var path = System.IO.Path.Combine(Path, name); var frames = (int)(sampleRate * seconds); sample ??= index => (float)Math.Sin(2 * Math.PI * 440 * index / sampleRate);
        using var stream = File.Create(path); using var writer = new BinaryWriter(stream, Encoding.ASCII, false); var dataLength = frames * channels * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataLength); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels);
        writer.Write(sampleRate); writer.Write(sampleRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataLength);
        for (var frame = 0; frame < frames; frame++) for (var channel = 0; channel < channels; channel++) writer.Write((short)(Math.Clamp(sample(frame), -1, 1) * short.MaxValue)); return path;
    }
    public string CreateCompressedContractFixture(string name) { var path = System.IO.Path.Combine(Path, name); File.WriteAllBytes(path, [0x56, 0x4e, 0x53, 0x01]); return path; }
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

internal sealed class SyntheticCompressedDecoder : IAudioDecoder
{
    public string BackendId => "synthetic-contract-1";
    public bool IsAvailable => true;
    public Task<AudioFormatInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return Task.FromResult(new AudioFormatInfo { Container = extension.TrimStart('.').ToUpperInvariant(), Codec = extension == ".mp3" ? "mp3" : "flac", SampleRate = 44100, ChannelCount = 2, BitDepth = extension == ".flac" ? 16 : null, BitRate = 128000, Duration = new AbsoluteTime(0.1m), Channels = [new(0, "Left"), new(1, "Right")] });
    }
    public async Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); await consumer(new(Enumerable.Repeat(0.25f, 8820).ToArray(), 44100, 2), cancellationToken); progress?.Report(1); }
}
