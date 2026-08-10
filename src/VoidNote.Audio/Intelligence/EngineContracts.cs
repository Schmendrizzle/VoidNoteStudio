using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;

namespace VoidNote.Audio.Intelligence;

public enum EngineInstallationState { Installed, Missing, IncompatibleVersion, WorkerStartFailed, ModelMissing }
public enum ComputeDevicePreference { Auto, Cpu, Gpu }
public enum AudioIntelligenceStage { Preparing, LoadingModel, Processing, WritingStems, ImportingResults, Completed, Failed, Cancelled }

public sealed record EngineCapabilities(
    bool SupportsCpu,
    bool SupportsGpu,
    bool IsGpuAvailable,
    IReadOnlyList<StemType> StemTypes,
    IReadOnlyList<TranscriptionMode> TranscriptionModes);

public sealed record EngineDiscoveryResult(
    string Engine,
    EngineInstallationState State,
    string? Version,
    string? ExecutablePath,
    EngineCapabilities Capabilities,
    string Message);

public sealed record AudioIntelligenceProgress(AudioIntelligenceStage Stage, double Fraction, string Message);

public sealed record SeparationRequest
{
    public required Guid JobId { get; init; }
    public required string InputPath { get; init; }
    public required AudioProcessingSource Source { get; init; }
    public required string OutputDirectory { get; init; }
    public string Model { get; init; } = "htdemucs";
    public ComputeDevicePreference Device { get; init; } = ComputeDevicePreference.Auto;
    public IReadOnlyList<StemType> RequestedStems { get; init; } = [StemType.Vocals, StemType.Bass, StemType.Drums, StemType.Other];
    public Dictionary<string, string> Settings { get; init; } = [];
}

public sealed record SeparatedStemFile(StemType Type, string? CustomType, string Name, string Path, AbsoluteTime Duration);
public sealed record SeparationResult(string Engine, string EngineVersion, IReadOnlyList<SeparatedStemFile> Stems, IReadOnlyDictionary<string, string> Metadata);

public sealed record TranscriptionRequest
{
    public required Guid JobId { get; init; }
    public required string InputPath { get; init; }
    public required AudioProcessingSource Source { get; init; }
    public required string OutputDirectory { get; init; }
    public TranscriptionMode Mode { get; init; } = TranscriptionMode.Auto;
    public ComputeDevicePreference Device { get; init; } = ComputeDevicePreference.Auto;
    public Dictionary<string, string> Settings { get; init; } = [];
}

public sealed record DetectedAudioNote(int Pitch, AbsoluteTime Start, AbsoluteTime Duration, decimal Velocity, decimal Confidence);
public sealed record TranscriptionEngineResult(string Engine, string EngineVersion, IReadOnlyList<DetectedAudioNote> Notes, IReadOnlyDictionary<string, string> Metadata);

public interface IAudioSeparationEngine
{
    string Id { get; }
    Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<SeparationResult> SeparateAsync(SeparationRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default);
}

public interface IAudioTranscriptionEngine
{
    string Id { get; }
    Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<TranscriptionEngineResult> TranscribeAsync(TranscriptionRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default);
}

public interface IAudioAnalysisEngine
{
    string Id { get; }
    Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
}
