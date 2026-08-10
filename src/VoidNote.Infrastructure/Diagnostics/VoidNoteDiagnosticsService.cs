using System.Diagnostics;
using System.Reflection;
using VoidNote.Application.Diagnostics;
using VoidNote.Application.Services;

namespace VoidNote.Infrastructure.Diagnostics;

/// <summary>Runs bounded, read-only local capability probes and never installs dependencies.</summary>
public sealed class VoidNoteDiagnosticsService(
    IAppPathProvider paths,
    string ffmpegPath,
    string ffplayPath,
    string pythonPath,
    string workerPath,
    Func<(bool Available, string Backend, string Description)> gameBridgeCapability) : IVoidNoteDiagnosticsService
{
    public async Task<VoidNoteDiagnosticReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<DiagnosticCheck>
        {
            new("dotnet", ".NET Runtime", DiagnosticState.Available, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription),
            new("os", "Operating system", DiagnosticState.Available, System.Runtime.InteropServices.RuntimeInformation.OSDescription),
        };
        checks.Add(await ProbeAsync("ffmpeg", "FFmpeg", ffmpegPath, ["-version"], cancellationToken));
        checks.Add(await ProbeAsync("ffplay", "FFplay", ffplayPath, ["-version"], cancellationToken));
        var python = await ProbeAsync("python", "Python", pythonPath, ["--version"], cancellationToken);
        checks.Add(python);
        checks.Add(File.Exists(workerPath)
            ? new("python-worker", "Python Worker", DiagnosticState.Available, "Worker script found.", Path: DisplayPath(workerPath))
            : new("python-worker", "Python Worker", DiagnosticState.Missing, "Worker script not found.", Path: DisplayPath(workerPath), Guidance: "Configure the worker path in Settings."));
        if (python.State == DiagnosticState.Available)
        {
            checks.Add(await ProbePythonPackageAsync("demucs", "Demucs", pythonPath, "demucs", cancellationToken));
            checks.Add(await ProbePythonPackageAsync("basic-pitch", "Basic Pitch", pythonPath, "basic-pitch", cancellationToken));
        }
        else
        {
            checks.Add(new("demucs", "Demucs", DiagnosticState.Missing, "Python is unavailable."));
            checks.Add(new("basic-pitch", "Basic Pitch", DiagnosticState.Missing, "Python is unavailable."));
        }
        checks.Add(CheckWritable("app-write", "Application data write permission", Path.GetDirectoryName(paths.SettingsFilePath)!));
        checks.Add(CheckWritable("temp", "Temporary directory", Path.GetTempPath()));
        checks.Add(new("audio", "Audio backend", checks.Single(value => value.Id == "ffplay").State,
            checks.Single(value => value.Id == "ffplay").State == DiagnosticState.Available ? "FFplay default-output backend available." : "Live preview unavailable; offline rendering remains available."));
        var bridge = gameBridgeCapability();
        checks.Add(new("gamebridge", "GameBridge capability", bridge.Available ? DiagnosticState.Available : DiagnosticState.NotApplicable, $"{bridge.Backend}: {bridge.Description}"));
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        return new(DateTimeOffset.UtcNow, version, checks);
    }

    private static async Task<DiagnosticCheck> ProbePythonPackageAsync(string id, string name, string python, string package, CancellationToken token)
    {
        const string script = "import importlib.metadata,sys; print(importlib.metadata.version(sys.argv[1]))";
        return await ProbeAsync(id, name, python, ["-c", script, package], token, "Install it manually in the configured Python environment.");
    }

    private static async Task<DiagnosticCheck> ProbeAsync(string id, string name, string executable, IReadOnlyList<string> arguments, CancellationToken token, string? guidance = null)
    {
        using var process = new Process { StartInfo = new() { FileName = executable, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            process.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            var outputTask = process.StandardOutput.ReadToEndAsync(linked.Token);
            var errorTask = process.StandardError.ReadToEndAsync(linked.Token);
            await process.WaitForExitAsync(linked.Token);
            var output = ((await outputTask) + " " + (await errorTask)).Trim();
            var firstLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? $"Exit code {process.ExitCode}";
            return new(id, name, process.ExitCode == 0 ? DiagnosticState.Available : DiagnosticState.Failed, firstLine,
                process.ExitCode == 0 ? ExtractVersion(firstLine) : null, DisplayPath(executable), guidance);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            return new(id, name, DiagnosticState.Failed, "Probe timed out.", Path: DisplayPath(executable), Guidance: guidance);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException)
        {
            return new(id, name, DiagnosticState.Missing, "Executable not found.", Path: DisplayPath(executable), Guidance: guidance);
        }
    }

    private static DiagnosticCheck CheckWritable(string id, string name, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $".voidnote-write-test-{Guid.NewGuid():N}");
            try { using (File.Create(path, 1, FileOptions.DeleteOnClose)) { } }
            finally { File.Delete(path); }
            return new(id, name, DiagnosticState.Available, "Writable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return new(id, name, DiagnosticState.Failed, "Not writable.", Guidance: "Choose a writable location or correct permissions."); }
    }

    private static string? DisplayPath(string path) => Path.IsPathRooted(path) ? Path.GetFullPath(path) : path;
    private static string? ExtractVersion(string text) => System.Text.RegularExpressions.Regex.Match(text, @"\d+(?:\.\d+){1,3}").Value is { Length: > 0 } value ? value : null;
}
