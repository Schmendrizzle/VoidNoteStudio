using VoidNote.Domain.Projects;

namespace VoidNote.Application.Projects;

public sealed record RecoveryCandidate(
    Guid ProjectId,
    string ProjectName,
    string? OriginalProjectPath,
    string AutosavePath,
    DateTimeOffset AutosavedAtUtc,
    DateTimeOffset? OriginalLastWriteTimeUtc);

/// <summary>Creates and discovers recovery snapshots without ever replacing the normal project file.</summary>
public interface IProjectRecoveryService
{
    Task<RecoveryCandidate> WriteAutosaveAsync(VoidNoteProject project, string? originalProjectPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryCandidate>> FindRecoverableAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryCandidate>> FindRecoverableAsync(IReadOnlyCollection<string> knownProjectPaths, CancellationToken cancellationToken = default);
    Task CompleteProjectSaveAsync(Guid projectId, string? previousProjectPath, string savedProjectPath, CancellationToken cancellationToken = default);
    Task CompleteCleanShutdownAsync(Guid projectId, string? projectPath, bool hasUnsavedChanges, CancellationToken cancellationToken = default);
    Task<VoidNoteProject> RecoverAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default);
    Task DiscardAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default);
}
