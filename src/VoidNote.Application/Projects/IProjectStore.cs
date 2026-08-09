using VoidNote.Domain.Projects;

namespace VoidNote.Application.Projects;

/// <summary>Persists the normalized VoidNote project model.</summary>
public interface IProjectStore
{
    /// <summary>Loads a versioned <c>.vns</c> project container.</summary>
    Task<VoidNoteProject> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Saves a versioned <c>.vns</c> project container atomically.</summary>
    Task SaveAsync(VoidNoteProject project, string path, CancellationToken cancellationToken = default);
}
