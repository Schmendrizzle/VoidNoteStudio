using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VoidNote.Audio.Intelligence;

namespace VoidNote.Audio.Tests;

public sealed class ProcessWorkerIntegrationTests
{
    [Fact]
    [Trait("Category", "ExternalWorker")]
    public async Task Cancellation_TerminatesWorkerAndItsChildProcess()
    {
        var python = Environment.GetEnvironmentVariable("VOIDNOTE_TEST_PYTHON");
        if (string.IsNullOrWhiteSpace(python) || !File.Exists(python)) return;
        using var fixture = new AudioFixtureDirectory();
        var script = Path.Combine(fixture.Path, "hanging_worker.py"); var pidPath = Path.Combine(fixture.Path, "pids.txt");
        await File.WriteAllTextAsync(script, """
import json, os, subprocess, sys, time
request = json.loads(sys.stdin.readline())
child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])
with open(request['input']['pidPath'], 'w', encoding='utf-8') as output:
    output.write(f'{os.getpid()},{child.pid}')
    output.flush()
time.sleep(60)
""");
        var client = new ProcessAudioWorkerClient(python, script, TimeSpan.FromMinutes(1), NullLogger<ProcessAudioWorkerClient>.Instance);
        var request = new WorkerRequest(1, Guid.NewGuid(), WorkerOperation.Discover, "test",
            JsonSerializer.SerializeToElement(new { pidPath }), JsonSerializer.SerializeToElement(new { }));
        using var cancellation = new CancellationTokenSource();
        var run = client.ExecuteAsync(request, cancellationToken: cancellation.Token);
        for (var attempt = 0; attempt < 100 && !File.Exists(pidPath); attempt++) await Task.Delay(10);
        Assert.True(File.Exists(pidPath)); var pids = (await File.ReadAllTextAsync(pidPath)).Split(',').Select(int.Parse).ToArray();

        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        for (var attempt = 0; attempt < 100 && pids.Any(IsRunning); attempt++) await Task.Delay(10);
        Assert.All(pids, pid => Assert.False(IsRunning(pid)));
    }

    private static bool IsRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
}
