using VoidNote.Application.Settings;

namespace VoidNote.Application.Projects;

/// <summary>Selects portable project-dialog folders without relying on the executable directory.</summary>
public static class ProjectDialogDirectories
{
    public static string GetPreferredDirectory(StorageSettings settings, string? documentsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return FirstUsable(settings.LastProjectDirectory, settings.DefaultProjectDirectory, documentsDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());
    }

    public static StorageSettings RememberProjectDirectory(StorageSettings settings, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? throw new ArgumentException("The project path has no parent directory.", nameof(projectPath));
        return settings with { LastProjectDirectory = directory };
    }

    private static string FirstUsable(params string?[] candidates) => candidates
        .First(value => !string.IsNullOrWhiteSpace(value))!;
}
