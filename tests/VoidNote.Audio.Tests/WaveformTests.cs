using Microsoft.Extensions.Logging.Abstractions;
using VoidNote.Audio.Decoding;
using VoidNote.Audio.Waveforms;

namespace VoidNote.Audio.Tests;

public sealed class WaveformTests
{
    [Fact]
    public async Task WaveDecoder_ProducesNormalizedSineSamplesInChunks()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("sine.wav", 8000, 1, 1);
        var chunks = new List<AudioPcmChunk>(); await new WaveAudioDecoder().DecodeAsync(new(path, Domain.Music.AbsoluteTime.Zero), (chunk, _) => { chunks.Add(chunk); return ValueTask.CompletedTask; });
        Assert.True(chunks.Count >= 2); Assert.All(chunks.SelectMany(value => value.Samples), value => Assert.InRange(value, -1f, 1f)); Assert.Equal(8000, chunks.Sum(value => value.FrameCount));
    }

    [Fact]
    public async Task WaveformPeaks_CaptureImpulseAndSilence()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("impulse.wav", 8000, 1, 0.1, index => index == 100 ? 1 : 0);
        var data = await Generator(fixtures).GetOrCreateAsync(path); var baseLevel = data.Levels[0];
        Assert.Contains(baseLevel.Peaks, value => value.Maximum > 0.99f); Assert.Contains(baseLevel.Peaks, value => value.Minimum == 0 && value.Maximum == 0);
    }

    [Fact]
    public async Task Waveform_CreatesMultipleZoomLevelsForLongAudio()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("long.wav", 8000, 2, 40);
        var data = await Generator(fixtures).GetOrCreateAsync(path); Assert.True(data.Levels.Count >= 2); Assert.True(data.Levels[1].FramesPerPeak > data.Levels[0].FramesPerPeak);
    }

    [Fact]
    public async Task Cache_RoundTripsPeakData()
    {
        using var fixtures = new AudioFixtureDirectory(); var cache = Cache(fixtures); var key = new WaveformCacheKey("roundtrip");
        var data = new WaveformData(8000, 1, 10, [new(2, 1, [new(-1, 1), new(-0.5f, 0.25f)])]); await cache.StoreAsync(key, data);
        var loaded = await cache.TryLoadAsync(key); Assert.NotNull(loaded); Assert.Equal(data.Levels[0].Peaks, loaded.Levels[0].Peaks);
    }

    [Fact]
    public async Task Cache_InvalidatesWhenFileIdentityChanges()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("changing.wav"); var decoder = new WaveAudioDecoder(); var info = await decoder.ProbeAsync(path);
        var before = WaveformGenerator.CreateKey(new(path), info, decoder.BackendId); await using (var stream = new FileStream(path, FileMode.Append)) await stream.WriteAsync(new byte[] { 0, 0 }); var after = WaveformGenerator.CreateKey(new(path), info, decoder.BackendId);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task CorruptCache_IsRejectedAndRemoved()
    {
        using var fixtures = new AudioFixtureDirectory(); var cache = Cache(fixtures); var key = new WaveformCacheKey("corrupt");
        await cache.StoreAsync(key, new(8000, 1, 1, [new(1, 1, [new(0, 0)])])); var file = Assert.Single(Directory.GetFiles(fixtures.Path, "*.vnwf")); await File.WriteAllBytesAsync(file, [1, 2, 3]);
        Assert.Null(await cache.TryLoadAsync(key)); Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task LargeFilePipeline_RetainsPeaksRatherThanFullPcm()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("minute.wav", 8000, 1, 60, _ => 0);
        var data = await Generator(fixtures).GetOrCreateAsync(path); Assert.Equal(480000, data.TotalFrames); Assert.True(data.Levels.Sum(level => level.Peaks.Count) < data.TotalFrames / 100);
    }

    [Fact]
    public async Task WaveformGeneration_ObservesCancellation()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("cancel.wav", 8000, 1, 2); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generator(fixtures).GetOrCreateAsync(path, cancellationToken: cancellation.Token));
    }

    private static FileWaveformCache Cache(AudioFixtureDirectory fixtures) => new(fixtures.Path, NullLogger<FileWaveformCache>.Instance);
    private static WaveformGenerator Generator(AudioFixtureDirectory fixtures) => new(new WaveAudioDecoder(), Cache(fixtures));
}
