using Microsoft.Extensions.Logging.Abstractions;
using VoidNote.Application.Jobs;
using VoidNote.Audio.Decoding;
using VoidNote.Audio.Playback;

namespace VoidNote.Audio.Tests;

public sealed class AudioInfrastructureTests
{
    [Fact]
    public async Task MissingFfmpeg_IsReportedWithoutAffectingWavDecoder()
    {
        var decoder = new FfmpegAudioDecoder(NullLogger<FfmpegAudioDecoder>.Instance, "definitely-missing-ffmpeg", "definitely-missing-ffprobe");
        Assert.False(decoder.IsAvailable); var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-sine.mp3");
        var exception = await Assert.ThrowsAsync<AudioDecoderException>(() => decoder.ProbeAsync(path)); Assert.Equal(AudioDecodeError.DecoderUnavailable, exception.Error); Assert.True(new WaveAudioDecoder().IsAvailable);
    }

    [Fact]
    public void MissingFfplay_ReportsTransparentDeviceCapability()
    {
        var device = new FfplayAudioDevice(NullLogger<FfplayAudioDevice>.Instance, "definitely-missing-ffplay"); Assert.False(device.Capability.IsAvailable); Assert.False(device.Capability.SupportsDeviceEnumeration);
    }

    [Fact]
    public async Task InstalledFfmpeg_ProbesAndDecodesRealCompressedFixtures()
    {
        var decoder = new FfmpegAudioDecoder(NullLogger<FfmpegAudioDecoder>.Instance); if (!decoder.IsAvailable) return;
        foreach (var name in new[] { "synthetic-sine.flac", "synthetic-sine.mp3" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name); Domain.Audio.AudioFormatInfo format;
            try { format = await decoder.ProbeAsync(path); } catch (AudioDecoderException exception) when (exception.Error == AudioDecodeError.DecoderUnavailable) { return; }
            long frames = 0;
            await decoder.DecodeAsync(new(path, Domain.Music.AbsoluteTime.Zero), (chunk, _) => { frames += chunk.FrameCount; return ValueTask.CompletedTask; });
            Assert.True(format.Duration.Seconds > 0); Assert.True(frames > 0);
        }
    }

    [Fact]
    public async Task JobManager_ReportsCompletionAndResult()
    {
        var jobs = new BackgroundJobManager(); var result = await jobs.RunAsync("Probe", (_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(42); });
        Assert.Equal(42, result); Assert.Equal(BackgroundJobState.Completed, Assert.Single(jobs.Jobs).State);
    }

    [Fact]
    public async Task JobManager_PropagatesCancellation()
    {
        var jobs = new BackgroundJobManager(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => jobs.RunAsync("Decode", (_, token) => Task.FromCanceled<int>(token), cancellation.Token)); Assert.Equal(BackgroundJobState.Cancelled, Assert.Single(jobs.Jobs).State);
    }
}
