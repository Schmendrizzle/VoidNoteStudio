namespace VoidNote.Audio.Intelligence;

public interface IAudioIntelligenceTempManager
{
    Task<string> CreateJobDirectoryAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task CleanupJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<int> CleanupOrphansAsync(TimeSpan minimumAge, CancellationToken cancellationToken = default);
}

/// <summary>Owns only VoidNote AI job directories and never deletes source audio.</summary>
public sealed class AudioIntelligenceTempManager(string rootDirectory) : IAudioIntelligenceTempManager
{
    private readonly string _root = Path.GetFullPath(rootDirectory);

    public Task<string> CreateJobDirectoryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = JobPath(jobId); Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, ".voidnote-ai-job"), DateTimeOffset.UtcNow.ToString("O"));
        return Task.FromResult(path);
    }

    public Task CleanupJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteOwnedDirectory(JobPath(jobId));
        return Task.CompletedTask;
    }

    public Task<int> CleanupOrphansAsync(TimeSpan minimumAge, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root)) return Task.FromResult(0);
        var removed = 0;
        foreach (var path in Directory.EnumerateDirectories(_root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var marker = Path.Combine(path, ".voidnote-ai-job");
            if (!File.Exists(marker) || DateTime.UtcNow - File.GetLastWriteTimeUtc(marker) < minimumAge) continue;
            DeleteOwnedDirectory(path); removed++;
        }
        return Task.FromResult(removed);
    }

    private string JobPath(Guid jobId) => Path.Combine(_root, jobId.ToString("N"));
    private void DeleteOwnedDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(full, ".voidnote-ai-job"))) return;
        Directory.Delete(full, recursive: true);
    }
}
