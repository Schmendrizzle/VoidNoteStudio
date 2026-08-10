namespace VoidNote.Audio.Waveforms;

public readonly record struct WaveformPeak(float Minimum, float Maximum);
public sealed record WaveformLevel(int FramesPerPeak, int ChannelCount, IReadOnlyList<WaveformPeak> Peaks)
{
    public int PeakFrameCount => Peaks.Count / ChannelCount;
}
public sealed record WaveformData(int SampleRate, int ChannelCount, long TotalFrames, IReadOnlyList<WaveformLevel> Levels)
{
    public WaveformLevel SelectLevel(int desiredPeakCount) => Levels.OrderBy(level => Math.Abs(level.PeakFrameCount - desiredPeakCount)).First();
}
public sealed record WaveformCacheKey(string Value);

public interface IWaveformCache
{
    Task<WaveformData?> TryLoadAsync(WaveformCacheKey key, CancellationToken cancellationToken = default);
    Task StoreAsync(WaveformCacheKey key, WaveformData data, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IWaveformGenerator
{
    Task<WaveformData> GetOrCreateAsync(string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
