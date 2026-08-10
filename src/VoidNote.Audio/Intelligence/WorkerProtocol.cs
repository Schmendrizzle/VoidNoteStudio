using System.Text.Json;

namespace VoidNote.Audio.Intelligence;

public static class AudioWorkerProtocol
{
    public const int CurrentVersion = 1;
}

public enum WorkerOperation { Discover, Separate, Transcribe }

public sealed record WorkerRequest(
    int ProtocolVersion,
    Guid JobId,
    WorkerOperation Operation,
    string Engine,
    JsonElement Input,
    JsonElement Settings);

public sealed record WorkerProgressMessage(int ProtocolVersion, Guid JobId, double Progress, AudioIntelligenceStage Stage, string Message);

public sealed record WorkerError(string Code, string Message, string? Details = null);

public sealed record WorkerResult(
    int ProtocolVersion,
    Guid JobId,
    bool Success,
    JsonElement Outputs,
    JsonElement Metrics,
    IReadOnlyList<WorkerError> Errors);

public sealed class AudioWorkerException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}

public interface IAudioWorkerClient
{
    Task<WorkerResult> ExecuteAsync(WorkerRequest request, IProgress<WorkerProgressMessage>? progress = null, CancellationToken cancellationToken = default);
}
