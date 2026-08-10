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
    private readonly Dictionary<string, RecoveryCandidate> _sessionAutosaves = new(PathComparer);

    public async Task<RecoveryCandidate> WriteAutosaveAsync(VoidNoteProject project, string? originalProjectPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(paths.RecoveryDirectory);
        var key = CreateKey(project.Id, originalProjectPath);
        var autosavePath = Path.Combine(paths.RecoveryDirectory, key + ".vns.autosave.vns");
        var metadataPath = MetadataPath(autosavePath);
        await projectStore.SaveAsync(project, autosavePath, cancellationToken).ConfigureAwait(false);
        var normalizedOriginal = NormalizeExistingPath(originalProjectPath);
        var autosavedAtUtc = DateTimeOffset.UtcNow;
        var candidate = new RecoveryCandidate(project.Id, project.Metadata.Title, normalizedOriginal, autosavePath,
            autosavedAtUtc, normalizedOriginal is not null && File.Exists(normalizedOriginal) ? GetLastWriteTimeUtc(normalizedOriginal) : null);
        await WriteMetadataAsync(metadataPath, candidate, cancellationToken).ConfigureAwait(false);
        _sessionAutosaves[Path.GetFullPath(autosavePath)] = candidate;
        return candidate;
    }

    public Task<IReadOnlyList<RecoveryCandidate>> FindRecoverableAsync(CancellationToken cancellationToken = default) =>
        FindRecoverableAsync([], cancellationToken);

    public async Task<IReadOnlyList<RecoveryCandidate>> FindRecoverableAsync(IReadOnlyCollection<string> knownProjectPaths, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.RecoveryDirectory)) return [];
        var knownProjects = await LoadKnownProjectsAsync(knownProjectPaths, cancellationToken).ConfigureAwait(false);
        var results = new List<RecoveryCandidate>();
        foreach (var metadataPath in Directory.EnumerateFiles(paths.RecoveryDirectory, "*.recovery.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                RecoveryCandidate? value;
                await using (var stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
                    value = await JsonSerializer.DeserializeAsync<RecoveryCandidate>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (value is null || !IsOwned(value.AutosavePath) || !PathEquals(metadataPath, MetadataPath(value.AutosavePath)) || !File.Exists(value.AutosavePath)) continue;

                var resolvedOriginal = await ResolveOriginalAsync(value, knownProjects, cancellationToken).ConfigureAwait(false);
                var originalWrite = resolvedOriginal is not null && File.Exists(resolvedOriginal)
                    ? GetLastWriteTimeUtc(resolvedOriginal)
                    : (DateTimeOffset?)null;
                var normalized = value with
                {
                    OriginalProjectPath = resolvedOriginal,
                    AutosavedAtUtc = value.AutosavedAtUtc.ToUniversalTime(),
                    OriginalLastWriteTimeUtc = originalWrite,
                };

                if (originalWrite is not null && !IsStrictlyNewer(normalized.AutosavedAtUtc, originalWrite.Value))
                {
                    DeleteRecoveryFiles(normalized);
                    continue;
                }

                if (!PathEquals(value.OriginalProjectPath, resolvedOriginal) && resolvedOriginal is not null)
                    await WriteMetadataAsync(metadataPath, normalized, cancellationToken).ConfigureAwait(false);
                results.Add(normalized);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException) { }
        }
        return results.OrderByDescending(value => value.AutosavedAtUtc).ToArray();
    }

    public Task CompleteProjectSaveAsync(Guid projectId, string? previousProjectPath, string savedProjectPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previous = NormalizeExistingPath(previousProjectPath);
        var saved = NormalizeExistingPath(savedProjectPath)!;
        foreach (var candidate in _sessionAutosaves.Values.Where(value => value.ProjectId == projectId &&
                     (PathEquals(value.OriginalProjectPath, previous) || PathEquals(value.OriginalProjectPath, saved))).ToArray())
        {
            DeleteRecoveryFiles(candidate);
            _sessionAutosaves.Remove(Path.GetFullPath(candidate.AutosavePath));
        }
        return Task.CompletedTask;
    }

    public Task CompleteCleanShutdownAsync(Guid projectId, string? projectPath, bool hasUnsavedChanges, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (hasUnsavedChanges) return Task.CompletedTask;
        foreach (var candidate in _sessionAutosaves.Values.Where(value => value.ProjectId == projectId).ToArray())
        {
            DeleteRecoveryFiles(candidate);
            _sessionAutosaves.Remove(Path.GetFullPath(candidate.AutosavePath));
        }
        return Task.CompletedTask;
    }

    public Task<VoidNoteProject> RecoverAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!IsOwned(candidate.AutosavePath)) throw new InvalidOperationException("Only VoidNote-owned recovery snapshots can be opened through crash recovery.");
        return projectStore.LoadAsync(candidate.AutosavePath, cancellationToken);
    }

    public Task DiscardAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        DeleteRecoveryFiles(candidate);
        _sessionAutosaves.Remove(Path.GetFullPath(candidate.AutosavePath));
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<KnownProject>> LoadKnownProjectsAsync(IReadOnlyCollection<string> projectPaths, CancellationToken token)
    {
        var result = new List<KnownProject>();
        foreach (var path in projectPaths.Where(value => !string.IsNullOrWhiteSpace(value)).Select(Path.GetFullPath).Distinct(PathComparer))
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;
            try
            {
                var project = await projectStore.LoadAsync(path, token).ConfigureAwait(false);
                result.Add(new KnownProject(project.Id, path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or NotSupportedException) { }
        }
        return result;
    }

    private async Task<string?> ResolveOriginalAsync(RecoveryCandidate candidate, IReadOnlyList<KnownProject> knownProjects, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(candidate.OriginalProjectPath))
        {
            var explicitPath = Path.GetFullPath(candidate.OriginalProjectPath);
            if (!File.Exists(explicitPath)) return explicitPath;
            try
            {
                var original = await projectStore.LoadAsync(explicitPath, token).ConfigureAwait(false);
                if (original.Id == candidate.ProjectId) return explicitPath;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or NotSupportedException) { }
        }

        var matches = knownProjects.Where(value => value.ProjectId == candidate.ProjectId).Select(value => value.Path).ToArray();
        return matches.Length == 1 ? matches[0] : null;
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
        if (IsOwned(path)) File.Delete(Path.GetFullPath(path));
    }

    private void DeleteRecoveryFiles(RecoveryCandidate candidate)
    {
        DeleteIfOwned(candidate.AutosavePath);
        DeleteIfOwned(MetadataPath(candidate.AutosavePath));
    }

    private bool IsOwned(string path)
    {
        var root = Path.GetFullPath(paths.RecoveryDirectory) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        return full.StartsWith(root, PathComparison);
    }

    private static string CreateKey(Guid projectId, string? path)
    {
        var input = projectId.ToString("N") + "|" + (path is null ? string.Empty : Path.GetFullPath(path));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..24];
    }

    private static string? NormalizeExistingPath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    private static string MetadataPath(string autosavePath) => Path.ChangeExtension(autosavePath, ".recovery.json");
    private static DateTimeOffset GetLastWriteTimeUtc(string path) => new(DateTime.SpecifyKind(File.GetLastWriteTimeUtc(path), DateTimeKind.Utc));
    private static bool IsStrictlyNewer(DateTimeOffset autosave, DateTimeOffset original) => autosave.UtcDateTime.Ticks > original.UtcDateTime.Ticks;
    private static bool PathEquals(string? left, string? right) => left is null || right is null ? left is null && right is null : string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private sealed record KnownProject(Guid ProjectId, string Path);
}
