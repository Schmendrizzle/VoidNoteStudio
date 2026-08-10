using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace VoidNote.Audio.Waveforms;

/// <summary>Versioned local binary peak cache. Corruption is logged, rejected and never silently reused.</summary>
public sealed class FileWaveformCache(string directory, ILogger<FileWaveformCache> logger) : IWaveformCache
{
    private const int Version = 1;
    private readonly string _directory = directory;

    public async Task<WaveformData?> TryLoadAsync(WaveformCacheKey key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key); if (!File.Exists(path)) return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (reader.ReadInt32() != 0x564E5746 || reader.ReadInt32() != Version || reader.ReadString() != key.Value) throw new InvalidDataException("Waveform cache header or key mismatch.");
            var rate = reader.ReadInt32(); var channels = reader.ReadInt32(); var frames = reader.ReadInt64(); var levelCount = reader.ReadInt32();
            if (rate <= 0 || channels <= 0 || frames < 0 || levelCount is <= 0 or > 64) throw new InvalidDataException("Invalid waveform cache metadata.");
            var levels = new List<WaveformLevel>(levelCount);
            for (var levelIndex = 0; levelIndex < levelCount; levelIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested(); var framesPerPeak = reader.ReadInt32(); var count = reader.ReadInt32();
                if (framesPerPeak <= 0 || count < 0 || count > 100_000_000) throw new InvalidDataException("Invalid waveform cache level.");
                var peaks = new WaveformPeak[count];
                for (var index = 0; index < count; index++) peaks[index] = new(reader.ReadSingle(), reader.ReadSingle());
                levels.Add(new(framesPerPeak, channels, peaks));
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected data follows the waveform cache.");
            await Task.CompletedTask; return new(rate, channels, frames, levels);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Ignoring corrupt waveform cache {Path}", path);
            try { File.Delete(path); } catch (IOException deleteError) { logger.LogWarning(deleteError, "Could not remove corrupt waveform cache {Path}", path); }
            return null;
        }
    }

    public async Task StoreAsync(WaveformCacheKey key, WaveformData data, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory); var path = PathFor(key); var temporary = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true); writer.Write(0x564E5746); writer.Write(Version); writer.Write(key.Value);
                writer.Write(data.SampleRate); writer.Write(data.ChannelCount); writer.Write(data.TotalFrames); writer.Write(data.Levels.Count);
                foreach (var level in data.Levels)
                {
                    cancellationToken.ThrowIfCancellationRequested(); writer.Write(level.FramesPerPeak); writer.Write(level.Peaks.Count);
                    foreach (var peak in level.Peaks) { writer.Write(peak.Minimum); writer.Write(peak.Maximum); }
                }
                writer.Flush(); await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory)) return Task.CompletedTask;
        foreach (var path in Directory.EnumerateFiles(_directory, "*.vnwf")) { cancellationToken.ThrowIfCancellationRequested(); File.Delete(path); }
        return Task.CompletedTask;
    }

    private string PathFor(WaveformCacheKey key) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Value))) + ".vnwf");
}
