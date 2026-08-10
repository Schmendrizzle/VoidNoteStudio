using VoidNote.Application.Commands;
using VoidNote.Domain.Projects;

namespace VoidNote.Application.Projects;

/// <summary>Applies project-name edits through the shared reversible command history.</summary>
public sealed class ProjectNameEditService(IUndoRedoService history)
{
    public event EventHandler? ProjectNameChanged;

    public void Rename(VoidNoteProject project, string name)
    {
        ArgumentNullException.ThrowIfNull(project);
        var normalized = ProjectName.Normalize(name);
        if (StringComparer.Ordinal.Equals(project.Metadata.Title, normalized)) return;
        history.Execute(new RenameProjectCommand(project, normalized, () => ProjectNameChanged?.Invoke(this, EventArgs.Empty)));
    }
}

/// <summary>Renames a project without coupling its display name to its file path.</summary>
public sealed class RenameProjectCommand(VoidNoteProject project, string name, Action? changed = null) : IUndoableCommand
{
    private readonly string _oldName = project.Metadata.Title;
    private readonly string _newName = ProjectName.Normalize(name);

    public string Description => "Rename project";

    public void Execute() => SetName(_newName);

    public void Undo() => SetName(_oldName);

    private void SetName(string value)
    {
        project.Metadata.Title = value;
        changed?.Invoke();
    }
}
