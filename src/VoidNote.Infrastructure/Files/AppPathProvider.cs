using VoidNote.Application.Services;

namespace VoidNote.Infrastructure.Files;

/// <summary>Maps VoidNote-owned data to the current operating system's local application-data folder.</summary>
public sealed class AppPathProvider : IAppPathProvider
{
    private readonly string _rootDirectory;

    /// <summary>Creates a path provider using the platform-neutral .NET application-data mapping.</summary>
    public AppPathProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    /// <summary>Creates a path provider rooted at an explicit directory, primarily for tests and portable hosts.</summary>
    public AppPathProvider(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _rootDirectory = Path.Combine(baseDirectory, "VoidNoteStudio");
    }

    /// <inheritdoc />
    public string SettingsFilePath => Path.Combine(_rootDirectory, "settings.json");

    /// <inheritdoc />
    public string LogDirectory => Path.Combine(_rootDirectory, "logs");
}
