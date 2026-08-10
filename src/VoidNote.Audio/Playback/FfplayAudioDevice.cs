using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using VoidNote.Audio.Decoding;

namespace VoidNote.Audio.Playback;

/// <summary>Cross-platform optional FFplay stdin backend. Device enumeration is intentionally reported as unavailable.</summary>
public sealed class FfplayAudioDevice(ILogger<FfplayAudioDevice> logger, string executable = "ffplay") : IAudioOutputDevice
{
    private Process? _process; private int _sampleRate; private int _channels;
    public string Id => "ffplay-default";
    public AudioDeviceCapability Capability => new(IsExecutableAvailable(executable), "FFplay 8.1.2", IsExecutableAvailable(executable) ? "Default system audio output through FFplay." : "FFplay is not installed; diagnostic playback remains available.", false);

    public Task StartAsync(int sampleRate, int channelCount, CancellationToken cancellationToken = default)
    {
        if (!Capability.IsAvailable) throw new AudioDecoderException(AudioDecodeError.DecoderUnavailable, Capability.Description);
        _sampleRate = sampleRate; _channels = channelCount;
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "-v", "error", "-nodisp", "-autoexit", "-f", "f32le", "-ar", sampleRate.ToString(CultureInfo.InvariantCulture), "-ch_layout", channelCount == 1 ? "mono" : "stereo", "-i", "pipe:0" }) info.ArgumentList.Add(argument);
        _process = Process.Start(info) ?? throw new InvalidOperationException("FFplay did not start."); logger.LogInformation("Audio playback started through FFplay at {SampleRate} Hz / {Channels} channels", sampleRate, channelCount); return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(AudioPcmChunk chunk, CancellationToken cancellationToken = default)
    {
        if (_process is null || _process.HasExited) throw new InvalidOperationException("The audio output device is not running.");
        if (chunk.SampleRate != _sampleRate || chunk.ChannelCount != _channels) throw new InvalidOperationException("PCM format changed during playback.");
        var bytes = new byte[chunk.Samples.Length * sizeof(float)]; Buffer.BlockCopy(chunk.Samples, 0, bytes, 0, bytes.Length);
        await _process.StandardInput.BaseStream.WriteAsync(bytes, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var process = _process; _process = null; if (process is null) return;
        try { process.StandardInput.Close(); await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
        finally { if (!process.HasExited) process.Kill(true); process.Dispose(); logger.LogInformation("Audio playback stopped"); }
    }

    public async ValueTask DisposeAsync() { try { await StopAsync(); } catch (InvalidOperationException) { } }
    internal static bool IsExecutableAvailable(string name) { if (Path.IsPathRooted(name)) return File.Exists(name); var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty; return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator).Any(path => File.Exists(Path.Combine(path, name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name : name + suffix))); }
}

public sealed class PlatformAudioDeviceProvider(ILoggerFactory loggerFactory, string executable = "ffplay") : IAudioDeviceProvider
{
    public AudioDeviceCapability Capability => new FfplayAudioDevice(loggerFactory.CreateLogger<FfplayAudioDevice>(), executable).Capability;
    public Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(Capability.IsAvailable ? [new("ffplay-default", "System default", true)] : []);
    public Task<IAudioOutputDevice> OpenDefaultAsync(CancellationToken cancellationToken = default)
    {
        IAudioOutputDevice device = new FfplayAudioDevice(loggerFactory.CreateLogger<FfplayAudioDevice>(), executable); return Task.FromResult(device);
    }
}

/// <summary>Offline output used by tests and diagnostics; it never opens a physical device.</summary>
public sealed class DiagnosticAudioOutputDevice : IAudioOutputDevice
{
    public string Id => "diagnostic";
    public AudioDeviceCapability Capability { get; } = new(true, "Diagnostic", "Consumes PCM without a physical audio device.", false);
    public long FramesWritten { get; private set; }
    public List<float> Samples { get; } = [];
    public Task StartAsync(int sampleRate, int channelCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask WriteAsync(AudioPcmChunk chunk, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); FramesWritten += chunk.FrameCount; Samples.AddRange(chunk.Samples); return ValueTask.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
