using Microsoft.Extensions.Logging.Abstractions;
using VoidNote.Audio.Decoding;
using VoidNote.Audio.Playback;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Tests;

public sealed class AudioPlaybackTests
{
    [Fact]
    public async Task PlaybackAtZero_StreamsToDiagnosticOutput()
    {
        var decoder = new CapturingDecoder(); var clock = new FakeClock(); var device = new DiagnosticAudioOutputDevice(); await using var engine = Engine(decoder, clock); var (project, track) = Project();
        await engine.LoadAsync(project, track, device); await engine.PlayAsync(); Assert.Equal(4, device.FramesWritten); Assert.Equal(AudioPlaybackState.Stopped, engine.State);
    }

    [Fact]
    public async Task TimelineOffset_UsesAbsoluteAnchorTarget()
    {
        var decoder = new CapturingDecoder(); var clock = new FakeClock(); await using var engine = Engine(decoder, clock); var (project, track) = Project(startTicks: 3840);
        await engine.LoadAsync(project, track, new DiagnosticAudioOutputDevice()); await engine.PlayAsync(); Assert.Contains(TimeSpan.FromSeconds(2), clock.Targets);
    }

    [Fact]
    public async Task Seek_MapsMasterPositionToSourceOffset()
    {
        var decoder = new CapturingDecoder(); await using var engine = Engine(decoder, new FakeClock()); var (project, track) = Project(duration: 5);
        await engine.LoadAsync(project, track, new DiagnosticAudioOutputDevice()); await engine.SeekAsync(new(2)); await engine.PlayAsync(); Assert.Equal(2, Assert.Single(decoder.Requests).Start.Seconds);
    }

    [Theory]
    [InlineData(0.5, false, false, 0.5f)]
    [InlineData(1.0, true, false, 0f)]
    [InlineData(1.0, false, true, 1f)]
    public async Task GainMuteAndSolo_AreApplied(decimal gain, bool mute, bool solo, float expected)
    {
        var decoder = new CapturingDecoder(); var device = new DiagnosticAudioOutputDevice(); await using var engine = Engine(decoder, new FakeClock()); var (project, track) = Project(); track.Gain = gain; track.IsMuted = mute; track.IsSolo = solo;
        await engine.LoadAsync(project, track, device); await engine.PlayAsync(); Assert.All(device.Samples, value => Assert.Equal(expected, value, 3));
    }

    [Fact]
    public async Task PauseResumeAndStopRestart_AreCancellable()
    {
        var decoder = new BlockingDecoder(); await using var engine = Engine(decoder, new FakeClock()); var (project, track) = Project(); var device = new DiagnosticAudioOutputDevice(); await engine.LoadAsync(project, track, device);
        _ = engine.PlayAsync(); await decoder.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); await engine.PauseAsync(); Assert.Equal(AudioPlaybackState.Paused, engine.State);
        decoder.Reset(); _ = engine.PlayAsync(); await decoder.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); await engine.StopAsync(); Assert.Equal(AudioPlaybackState.Stopped, engine.State); Assert.Equal(AbsoluteTime.Zero, engine.Position);
    }

    [Fact]
    public async Task MultipleLongOffsets_DoNotAccumulatePreviousWaits()
    {
        var decoder = new CapturingDecoder(); var clock = new FakeClock(); await using var engine = Engine(decoder, clock); var (project, track) = Project(duration: 1);
        var sourceId = track.Clips[0].SourceId; track.Clips.Add(new() { Name = "Ten", SourceId = sourceId, Start = project.Timeline.ToMusicalTime(new(10)), Duration = new(1) }); track.Clips.Add(new() { Name = "Twenty", SourceId = sourceId, Start = project.Timeline.ToMusicalTime(new(20)), Duration = new(1) });
        await engine.LoadAsync(project, track, new DiagnosticAudioOutputDevice()); await engine.PlayAsync();
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)], clock.Targets); Assert.Equal(20, clock.Targets[^1].TotalSeconds);
    }

    [Fact]
    public async Task DiagnosticOutput_RequiresNoPhysicalDevice()
    {
        await using var device = new DiagnosticAudioOutputDevice(); await device.StartAsync(44100, 2); await device.WriteAsync(new([0, 0, 1, -1], 44100, 2)); await device.StopAsync(); Assert.Equal(2, device.FramesWritten); Assert.Equal("Diagnostic", device.Capability.Backend);
    }

    [Fact]
    public async Task StemMixPreview_PlaysAllAudibleStemsAndHonorsMute()
    {
        var decoder = new CapturingDecoder(); var provider = new DiagnosticProvider();
        await using var preview = new AudioStemMixPreview(decoder, new FakeClock(), provider, NullLoggerFactory.Instance);
        var (project, first) = Project(); var second = new AudioTrack { Name = "Bass", Clips = [new() { Name = "Bass", SourceId = first.Clips[0].SourceId, Duration = new(1) }] };
        var muted = new AudioTrack { Name = "Drums", IsMuted = true, Clips = [new() { Name = "Drums", SourceId = first.Clips[0].SourceId, Duration = new(1) }] };
        project.AudioTracks.AddRange([second, muted]);

        await preview.PlayAsync(project, [first, second, muted]);

        Assert.Equal(2, provider.Devices.Count); Assert.All(provider.Devices, device => Assert.Equal(4, device.FramesWritten));
    }

    private static AudioPlaybackEngine Engine(IAudioDecoder decoder, IAudioPlaybackClock clock) => new(decoder, clock, NullLogger<AudioPlaybackEngine>.Instance);
    private static (VoidNoteProject Project, AudioTrack Track) Project(long startTicks = 0, decimal duration = 1)
    {
        var source = new AudioSource { Name = "Source", SourcePath = "synthetic.wav", ResolvedPath = "synthetic.wav", File = new("synthetic.wav", ProjectPathKind.Relative), Format = new() { Container = "WAV", Codec = "PCM", SampleRate = 4, ChannelCount = 1, BitDepth = 16, Duration = new(duration) } };
        var track = new AudioTrack { Name = "Audio", Clips = [new() { Name = "Clip", SourceId = source.Id, Start = new(startTicks), Duration = new(duration) }] }; return (new() { AudioSources = [source], AudioTracks = [track] }, track);
    }

    private sealed class FakeClock : IAudioPlaybackClock
    {
        public List<TimeSpan> Targets { get; } = []; private TimeSpan _elapsed;
        public long GetTimestamp() => 1;
        public TimeSpan GetElapsedTime(long startTimestamp) => _elapsed;
        public Task DelayUntilAsync(long anchorTimestamp, TimeSpan target, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Targets.Add(target); _elapsed = target; return Task.CompletedTask; }
    }

    private class CapturingDecoder : IAudioDecoder
    {
        public string BackendId => "capture"; public bool IsAvailable => true; public List<AudioDecodeRequest> Requests { get; } = [];
        public Task<AudioFormatInfo> ProbeAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(new AudioFormatInfo { Container = "WAV", Codec = "PCM", SampleRate = 4, ChannelCount = 1, Duration = new(1) });
        public virtual async Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer, IProgress<double>? progress = null, CancellationToken cancellationToken = default) { Requests.Add(request); await consumer(new([1, 1, 1, 1], 4, 1), cancellationToken); }
    }

    private sealed class BlockingDecoder : CapturingDecoder
    {
        public TaskCompletionSource Started { get; private set; } = NewSource();
        public override async Task DecodeAsync(AudioDecodeRequest request, Func<AudioPcmChunk, CancellationToken, ValueTask> consumer, IProgress<double>? progress = null, CancellationToken cancellationToken = default) { Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
        public void Reset() => Started = NewSource();
        private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class DiagnosticProvider : IAudioDeviceProvider
    {
        public List<DiagnosticAudioOutputDevice> Devices { get; } = [];
        public AudioDeviceCapability Capability { get; } = new(true, "Diagnostic", "In-memory", false);
        public Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);
        public Task<IAudioOutputDevice> OpenDefaultAsync(CancellationToken cancellationToken = default)
        { var device = new DiagnosticAudioOutputDevice(); Devices.Add(device); return Task.FromResult<IAudioOutputDevice>(device); }
    }
}
