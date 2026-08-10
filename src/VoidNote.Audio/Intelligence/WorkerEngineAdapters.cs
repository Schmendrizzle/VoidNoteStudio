using System.Text.Json;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;

namespace VoidNote.Audio.Intelligence;

public abstract class WorkerEngineAdapter(IAudioWorkerClient worker)
{
    protected IAudioWorkerClient Worker { get; } = worker;

    protected async Task<EngineDiscoveryResult> DiscoverAsync(string engine, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        try
        {
            var result = await Worker.ExecuteAsync(new(
                AudioWorkerProtocol.CurrentVersion, jobId, WorkerOperation.Discover, engine,
                JsonSerializer.SerializeToElement(new { }), JsonSerializer.SerializeToElement(new { })), cancellationToken: cancellationToken);
            var output = result.Outputs;
            var installed = output.TryGetProperty("installed", out var installedValue) && installedValue.GetBoolean();
            var version = output.TryGetProperty("version", out var versionValue) ? versionValue.GetString() : null;
            var modelAvailable = !output.TryGetProperty("modelAvailable", out var modelValue) || modelValue.GetBoolean();
            var gpu = output.TryGetProperty("gpuAvailable", out var gpuValue) && gpuValue.GetBoolean();
            var state = !installed ? EngineInstallationState.Missing : !IsCompatible(engine, version) ? EngineInstallationState.IncompatibleVersion
                : !modelAvailable ? EngineInstallationState.ModelMissing : EngineInstallationState.Installed;
            return new(engine, state, version, output.TryGetProperty("executablePath", out var path) ? path.GetString() : null,
                new(true, engine == "demucs", gpu, engine == "demucs" ? [StemType.Vocals, StemType.Bass, StemType.Drums, StemType.Other] : [],
                    engine == "basic-pitch" ? [TranscriptionMode.Auto, TranscriptionMode.Monophonic, TranscriptionMode.Polyphonic] : []),
                output.TryGetProperty("message", out var message) ? message.GetString() ?? state.ToString() : state.ToString());
        }
        catch (AudioWorkerException exception)
        {
            var state = exception.Code == "worker_missing" ? EngineInstallationState.Missing : EngineInstallationState.WorkerStartFailed;
            return new(engine, state, null, null, new(true, false, false, [], []), exception.Message);
        }
    }

    private static bool IsCompatible(string engine, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        var numeric = version.Split('-', '+')[0];
        if (!Version.TryParse(numeric, out var parsed)) return false;
        return engine switch { "demucs" => parsed.Major == 4, "basic-pitch" => parsed.Major == 0 && parsed.Minor >= 4, _ => true };
    }

    protected static IProgress<WorkerProgressMessage>? Bridge(Guid jobId, IProgress<AudioIntelligenceProgress>? progress) => progress is null ? null :
        new Progress<WorkerProgressMessage>(value =>
        {
            if (value.JobId == jobId) progress.Report(new(value.Stage, Math.Clamp(value.Progress, 0, 1), value.Message));
        });
}

/// <summary>Demucs 4.x adapter; all package and model access remains inside the external worker.</summary>
public sealed class DemucsSeparationEngine(IAudioWorkerClient worker) : WorkerEngineAdapter(worker), IAudioSeparationEngine
{
    public string Id => "demucs";
    public Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => DiscoverAsync(Id, cancellationToken);

    public async Task<SeparationResult> SeparateAsync(SeparationRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.SerializeToElement(new
        {
            path = request.InputPath,
            outputDirectory = request.OutputDirectory,
            startSeconds = request.Source.SourceOffset.Seconds,
            durationSeconds = request.Source.Duration.Seconds,
        });
        var settings = JsonSerializer.SerializeToElement(new
        {
            model = request.Model,
            device = request.Device.ToString().ToLowerInvariant(),
            requestedStems = request.RequestedStems.Select(value => value.ToString()).ToArray(),
            values = request.Settings,
        });
        var result = await Worker.ExecuteAsync(new(AudioWorkerProtocol.CurrentVersion, request.JobId, WorkerOperation.Separate, Id, input, settings), Bridge(request.JobId, progress), cancellationToken);
        var version = result.Outputs.GetProperty("version").GetString() ?? "unknown";
        var stems = result.Outputs.GetProperty("stems").EnumerateArray().Select(value =>
        {
            var rawType = value.GetProperty("type").GetString() ?? "Other";
            var type = Enum.TryParse<StemType>(rawType, true, out var parsed) ? parsed : StemType.Custom;
            return new SeparatedStemFile(type, type == StemType.Custom ? rawType : null, value.GetProperty("name").GetString() ?? rawType,
                value.GetProperty("path").GetString() ?? throw new AudioWorkerException("invalid_result", "A stem path is missing."),
                new AbsoluteTime(value.GetProperty("durationSeconds").GetDecimal()));
        }).ToArray();
        if (stems.Count(value => value.Type is StemType.Vocals or StemType.Bass or StemType.Drums or StemType.Other) < 4)
            throw new AudioWorkerException("invalid_result", "Demucs did not return all four standard stems.");
        return new(Id, version, stems, ReadMetadata(result.Metrics));
    }

    private static IReadOnlyDictionary<string, string> ReadMetadata(JsonElement element) => element.ValueKind == JsonValueKind.Object
        ? element.EnumerateObject().ToDictionary(value => value.Name, value => value.Value.ToString()) : new Dictionary<string, string>();
}

/// <summary>Spotify Basic Pitch adapter. Drum material is rejected instead of producing misleading pitched notes.</summary>
public sealed class BasicPitchTranscriptionEngine(IAudioWorkerClient worker) : WorkerEngineAdapter(worker), IAudioTranscriptionEngine
{
    public string Id => "basic-pitch";
    public Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => DiscoverAsync(Id, cancellationToken);

    public async Task<TranscriptionEngineResult> TranscribeAsync(TranscriptionRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (request.Device == ComputeDevicePreference.Gpu)
            throw new AudioWorkerException("unsupported_device", "The Basic Pitch adapter does not expose explicit GPU selection; choose Auto or CPU.");
        if (request.Source.StemId.HasValue && request.Settings.TryGetValue("stemType", out var stemType) && string.Equals(stemType, "Drums", StringComparison.OrdinalIgnoreCase))
            throw new AudioWorkerException("unsupported_audio", "Basic Pitch does not provide meaningful pitched drum transcription.");
        var input = JsonSerializer.SerializeToElement(new
        {
            path = request.InputPath,
            outputDirectory = request.OutputDirectory,
            startSeconds = request.Source.SourceOffset.Seconds,
            durationSeconds = request.Source.Duration.Seconds,
        });
        var settings = JsonSerializer.SerializeToElement(new
        {
            mode = request.Mode.ToString().ToLowerInvariant(), device = request.Device.ToString().ToLowerInvariant(), values = request.Settings,
        });
        var result = await Worker.ExecuteAsync(new(AudioWorkerProtocol.CurrentVersion, request.JobId, WorkerOperation.Transcribe, Id, input, settings), Bridge(request.JobId, progress), cancellationToken);
        var version = result.Outputs.GetProperty("version").GetString() ?? "unknown";
        var notes = result.Outputs.GetProperty("notes").EnumerateArray().Select(value => new DetectedAudioNote(
            value.GetProperty("pitch").GetInt32(), new(value.GetProperty("startSeconds").GetDecimal()),
            new(value.GetProperty("durationSeconds").GetDecimal()), value.GetProperty("velocity").GetDecimal(),
            value.GetProperty("confidence").GetDecimal())).ToArray();
        if (notes.Any(note => note.Pitch is < 0 or > 127 || note.Confidence is < 0 or > 1 || note.Duration.Seconds <= 0))
            throw new AudioWorkerException("invalid_result", "Basic Pitch returned an invalid note.");
        var metadata = result.Metrics.ValueKind == JsonValueKind.Object
            ? result.Metrics.EnumerateObject().ToDictionary(value => value.Name, value => value.Value.ToString()) : [];
        return new(Id, version, notes, metadata);
    }
}
