using System.Globalization;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;

namespace VoidNote.Audio.Waveforms;

/// <summary>Builds a multiresolution min/max pyramid while retaining only bounded PCM chunks.</summary>
public sealed class WaveformGenerator(IAudioDecoder decoder, IWaveformCache cache) : IWaveformGenerator
{
    private const int BaseFramesPerPeak = 256;

    public async Task<WaveformData> GetOrCreateAsync(string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var format = await decoder.ProbeAsync(path, cancellationToken); var file = new FileInfo(path);
        var key = CreateKey(file, format, decoder.BackendId); var cached = await cache.TryLoadAsync(key, cancellationToken);
        if (cached is not null) { progress?.Report(1); return cached; }
        var builder = new PeakBuilder(format.ChannelCount, BaseFramesPerPeak);
        var decodeProgress = new Progress<double>(value => progress?.Report(value * 0.9));
        await decoder.DecodeAsync(new(path, Domain.Music.AbsoluteTime.Zero), (chunk, token) => { builder.Add(chunk.Samples, token); return ValueTask.CompletedTask; }, decodeProgress, cancellationToken);
        var baseLevel = builder.Complete(); var levels = new List<WaveformLevel> { baseLevel };
        while (levels[^1].PeakFrameCount > 1024) levels.Add(Coarsen(levels[^1]));
        var data = new WaveformData(format.SampleRate, format.ChannelCount, builder.TotalFrames, levels);
        progress?.Report(0.95); await cache.StoreAsync(key, data, cancellationToken); progress?.Report(1); return data;
    }

    public static WaveformCacheKey CreateKey(FileInfo file, AudioFormatInfo format, string decoderId) => new(string.Join('|',
        Path.GetFullPath(file.FullName), file.Length.ToString(CultureInfo.InvariantCulture), file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture), decoderId,
        format.Codec, format.SampleRate.ToString(CultureInfo.InvariantCulture), format.ChannelCount.ToString(CultureInfo.InvariantCulture), format.BitDepth?.ToString(CultureInfo.InvariantCulture) ?? "unknown"));

    private static WaveformLevel Coarsen(WaveformLevel source)
    {
        var frames = (source.PeakFrameCount + 1) / 2; var peaks = new WaveformPeak[frames * source.ChannelCount];
        for (var frame = 0; frame < frames; frame++) for (var channel = 0; channel < source.ChannelCount; channel++)
        {
            var first = source.Peaks[(frame * 2) * source.ChannelCount + channel];
            var secondIndex = (frame * 2 + 1) * source.ChannelCount + channel; var second = secondIndex < source.Peaks.Count ? source.Peaks[secondIndex] : first;
            peaks[frame * source.ChannelCount + channel] = new(Math.Min(first.Minimum, second.Minimum), Math.Max(first.Maximum, second.Maximum));
        }
        return new(source.FramesPerPeak * 2, source.ChannelCount, peaks);
    }

    private sealed class PeakBuilder
    {
        private readonly int _channels; private readonly int _framesPerPeak; private readonly List<WaveformPeak> _peaks = [];
        private readonly float[] _minimum; private readonly float[] _maximum; private int _framesInPeak;
        public PeakBuilder(int channels, int framesPerPeak) { _channels = channels; _framesPerPeak = framesPerPeak; _minimum = Enumerable.Repeat(float.PositiveInfinity, channels).ToArray(); _maximum = Enumerable.Repeat(float.NegativeInfinity, channels).ToArray(); }
        public long TotalFrames { get; private set; }
        public void Add(float[] samples, CancellationToken token)
        {
            for (var offset = 0; offset < samples.Length; offset += _channels)
            {
                token.ThrowIfCancellationRequested(); for (var channel = 0; channel < _channels; channel++) { var value = Math.Clamp(samples[offset + channel], -1, 1); _minimum[channel] = Math.Min(_minimum[channel], value); _maximum[channel] = Math.Max(_maximum[channel], value); }
                _framesInPeak++; TotalFrames++; if (_framesInPeak == _framesPerPeak) Flush();
            }
        }
        public WaveformLevel Complete() { if (_framesInPeak > 0) Flush(); if (_peaks.Count == 0) for (var channel = 0; channel < _channels; channel++) _peaks.Add(new(0, 0)); return new(_framesPerPeak, _channels, _peaks.ToArray()); }
        private void Flush() { for (var channel = 0; channel < _channels; channel++) { _peaks.Add(new(_minimum[channel], _maximum[channel])); _minimum[channel] = float.PositiveInfinity; _maximum[channel] = float.NegativeInfinity; } _framesInPeak = 0; }
    }
}
