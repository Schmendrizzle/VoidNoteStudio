using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VoidNote.Application.Projects;
using VoidNote.Application.Services;
using VoidNote.Domain.Projects;

namespace VoidNote.Infrastructure.Projects;

/// <summary>Stores each recovery snapshot in the application data directory alongside minimal metadata.</summary>
public sealed class ProjectRecoveryService(IProjectStore projectStore, IAppPathProvider paths) : IProjectRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    public async Task<RecoveryCandidate> WriteAutosaveAsync(VoidNoteProject project, string? originalProjectPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(paths.RecoveryDirectory);
        var key = CreateKey(project.Id, originalProjectPath);
        var autosavePath = Path.Combine(paths.RecoveryDirectory, key + ".vns.autosave.vns");
        var metadataPath = MetadataPath(autosavePath);
        await projectStore.SaveAsync(project, autosavePath, cancellationToken).ConfigureAwait(false);
        var normalizedOriginal = NormalizeExistingPath(originalProjectPath);
        var candidate = new RecoveryCandidate(project.Id, project.Metadata.Title, normalizedOriginal, autosavePath,
            DateTimeOffset.UtcNow, normalizedOriginal is not null && File.Exists(normalizedOriginal) ? File.GetLastWriteTimeUtc(normalizedOriginal) : null);
        await WriteMetadataAsync(metadataPath, candidate, cancellationToken).ConfigureAwait(false);
        return candidate;
    }

    public async Task<IReadOnlyList<RecoveryCandidate>> FindRecoverableAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.RecoveryDirectory)) return [];
        var results = new List<RecoveryCandidate>();
        foreach (var metadataPath in Directory.EnumerateFiles(paths.RecoveryDirectory, "*.recovery.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                var value = await JsonSerializer.DeserializeAsync<RecoveryCandidate>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (value is null || !File.Exists(value.AutosavePath)) continue;
                var originalWrite = value.OriginalProjectPath is not null && File.Exists(value.OriginalProjectPath)
                    ? new DateTimeOffset(File.GetLastWriteTimeUtc(value.OriginalProjectPath), TimeSpan.Zero)
                    : (DateTimeOffset?)null;
                if (originalWrite is null || value.AutosavedAtUtc > originalWrite) results.Add(value);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return results.OrderByDescending(value => value.AutosavedAtUtc).ToArray();
    }

    public Task<VoidNoteProject> RecoverAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return projectStore.LoadAsync(candidate.AutosavePath, cancellationToken);
    }

    public Task DiscardAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfOwned(candidate.AutosavePath);
        DeleteIfOwned(MetadataPath(candidate.AutosavePath));
        return Task.CompletedTask;
    }

    private async Task WriteMetadataAsync(string path, RecoveryCandidate candidate, CancellationToken token)
    {
        var temporary = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, candidate, JsonOptions, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }

    private void DeleteIfOwned(string path)
    {
        var root = Path.GetFullPath(paths.RecoveryDirectory) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        if (full.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) File.Delete(full);
    }

    private static string CreateKey(Guid projectId, string? path)
    {
        var input = projectId.ToString("N") + "|" + (path is null ? string.Empty : Path.GetFullPath(path));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..24];
    }

    private static string? NormalizeExistingPath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    private static string MetadataPath(string autosavePath) => Path.ChangeExtension(autosavePath, ".recovery.json");
}
