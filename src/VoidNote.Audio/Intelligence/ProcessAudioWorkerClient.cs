using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VoidNote.Audio.Intelligence;

/// <summary>Runs one isolated local worker process per request and guarantees process-tree cleanup.</summary>
public sealed class ProcessAudioWorkerClient(
    string executablePath,
    string workerScriptPath,
    TimeSpan timeout,
    ILogger<ProcessAudioWorkerClient> logger) : IAudioWorkerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    static ProcessAudioWorkerClient() => JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    public async Task<WorkerResult> ExecuteAsync(WorkerRequest request, IProgress<WorkerProgressMessage>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(workerScriptPath)) throw new AudioWorkerException("worker_missing", $"Audio worker script not found: {workerScriptPath}");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        process.StartInfo.ArgumentList.Add(workerScriptPath);
        try
        {
            if (!process.Start()) throw new AudioWorkerException("worker_start_failed", "The audio worker did not start.");
        }
        catch (Exception exception) when (exception is not AudioWorkerException)
        {
            throw new AudioWorkerException("worker_start_failed", $"The audio worker could not start: {exception.Message}", exception);
        }

        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        var stderr = process.StandardError.ReadToEndAsync(linked.Token);
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions).AsMemory(), linked.Token);
            process.StandardInput.Close();
            while (await process.StandardOutput.ReadLineAsync(linked.Token) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var document = JsonDocument.Parse(line);
                var kind = document.RootElement.GetProperty("kind").GetString();
                if (kind == "progress")
                {
                    var message = document.RootElement.Deserialize<WorkerProgressMessage>(JsonOptions)
                        ?? throw new AudioWorkerException("invalid_result", "The worker returned invalid progress data.");
                    Validate(message.ProtocolVersion, message.JobId, request);
                    progress?.Report(message);
                }
                else if (kind == "result")
                {
                    var result = document.RootElement.Deserialize<WorkerResult>(JsonOptions)
                        ?? throw new AudioWorkerException("invalid_result", "The worker returned an empty result.");
                    Validate(result.ProtocolVersion, result.JobId, request);
                    await process.WaitForExitAsync(linked.Token);
                    if (!result.Success)
                    {
                        var error = result.Errors.FirstOrDefault() ?? new("worker_failed", "The worker operation failed.");
                        throw new AudioWorkerException(error.Code, error.Message);
                    }
                    return result;
                }
                else throw new AudioWorkerException("invalid_result", "The worker returned an unknown message kind.");
            }
            await process.WaitForExitAsync(linked.Token);
            var details = await stderr;
            throw new AudioWorkerException("worker_crash", $"The audio worker exited without a result (code {process.ExitCode}). {details}".Trim());
        }
        catch (JsonException exception)
        {
            Kill(process);
            throw new AudioWorkerException("invalid_result", "The audio worker returned malformed JSON.", exception);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            Kill(process);
            if (timeoutCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                throw new AudioWorkerException("timeout", "The audio worker exceeded its configured timeout.");
            throw;
        }
        catch
        {
            Kill(process);
            throw;
        }
        finally
        {
            if (!process.HasExited) Kill(process);
            try
            {
                var diagnostic = await stderr;
                if (!string.IsNullOrWhiteSpace(diagnostic)) logger.LogDebug("Audio worker diagnostics: {Diagnostic}", diagnostic);
            }
            catch (OperationCanceledException) { }
        }
    }

    private static void Validate(int protocolVersion, Guid jobId, WorkerRequest request)
    {
        if (protocolVersion != AudioWorkerProtocol.CurrentVersion || jobId != request.JobId)
            throw new AudioWorkerException("invalid_result", "The worker result has an incompatible protocol version or job ID.");
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
