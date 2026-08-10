using VoidNote.Audio.Decoding;

namespace VoidNote.Audio.Playback;

public sealed record AudioDeviceCapability(bool IsAvailable, string Backend, string Description, bool SupportsDeviceEnumeration);
public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault);

public interface IAudioOutputDevice : IAsyncDisposable
{
    string Id { get; }
    AudioDeviceCapability Capability { get; }
    Task StartAsync(int sampleRate, int channelCount, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(AudioPcmChunk chunk, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IAudioDeviceProvider
{
    AudioDeviceCapability Capability { get; }
    Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<IAudioOutputDevice> OpenDefaultAsync(CancellationToken cancellationToken = default);
}

public interface IAudioPlaybackClock
{
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startTimestamp);
    Task DelayUntilAsync(long anchorTimestamp, TimeSpan target, CancellationToken cancellationToken);
}

public sealed class SystemAudioPlaybackClock : IAudioPlaybackClock
{
    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();
    public TimeSpan GetElapsedTime(long startTimestamp) => System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);
    public async Task DelayUntilAsync(long anchorTimestamp, TimeSpan target, CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = target - GetElapsedTime(anchorTimestamp); if (remaining <= TimeSpan.Zero) return;
            await Task.Delay(remaining > TimeSpan.FromMilliseconds(20) ? remaining - TimeSpan.FromMilliseconds(5) : remaining, cancellationToken);
        }
    }
}
