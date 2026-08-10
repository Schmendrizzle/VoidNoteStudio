using VoidNote.Application.Commands;
using VoidNote.Application.Projects;
using VoidNote.Application.Settings;
using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Projects;
using VoidNote.Infrastructure.Settings;

namespace VoidNote.IntegrationTests;

public sealed class ProjectUxTests
{
    [Fact]
    public void RenameProject_MarksEditingSessionModifiedAndSupportsUndoRedo()
    {
        var project = new VoidNoteProject();
        var history = new UndoRedoService();
        var editor = new ProjectNameEditService(history);
        var modified = false;
        editor.ProjectNameChanged += (_, _) => modified = true;

        editor.Rename(project, "Actual project name");

        Assert.True(modified);
        Assert.Equal("Actual project name", project.Metadata.Title);
        Assert.True(history.Undo());
        Assert.Equal(ProjectName.Default, project.Metadata.Title);
        Assert.True(history.Redo());
        Assert.Equal("Actual project name", project.Metadata.Title);
    }

    [Fact]
    public async Task ProjectName_SaveLoadRoundTrip_IsIndependentOfVnsFileName()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "different-file-name.vns");
        var project = new VoidNoteProject { Metadata = new() { Title = "My / Display: Name?" } };
        var store = new VnsProjectStore();

        await store.SaveAsync(project, path);
        var loaded = await store.LoadAsync(path);

        Assert.Equal("My / Display: Name?", loaded.Metadata.Title);
        Assert.NotEqual(Path.GetFileNameWithoutExtension(path), loaded.Metadata.Title);
    }

    [Fact]
    public void RecentProject_UsesActualProjectNameInsteadOfFileName()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "opaque-file-name.vns"));
        var recent = RecentProjects.AddOrUpdate([], "Displayed project name", path, DateTimeOffset.UtcNow);

        Assert.Equal("Displayed project name", Assert.Single(recent).Name);
    }

    [Fact]
    public void DefaultDialogDirectory_UsesDocumentsAndNotExecutableDirectory()
    {
        var documents = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "VoidNote-Documents"));

        var actual = ProjectDialogDirectories.GetPreferredDirectory(new StorageSettings(), documents);

        Assert.Equal(documents, actual);
        Assert.NotEqual(AppContext.BaseDirectory, actual);
    }

    [Theory]
    [InlineData("C:\\Users\\Example\\Documents")]
    [InlineData("/home/example/Documents")]
    public void DialogDirectoryPreference_IsOperatingSystemNeutral(string configuredDirectory)
    {
        var settings = new StorageSettings { LastProjectDirectory = configuredDirectory };

        Assert.Equal(configuredDirectory, ProjectDialogDirectories.GetPreferredDirectory(settings, "/fallback/Documents"));
    }

    [Fact]
    public async Task LastProjectDirectory_IsRememberedAndPersisted()
    {
        using var directory = new TemporaryDirectory();
        var projectDirectory = Path.Combine(directory.Path, "Projects");
        var projectPath = Path.Combine(projectDirectory, "song.vns");
        var storage = ProjectDialogDirectories.RememberProjectDirectory(new StorageSettings(), projectPath);
        var store = new JsonSettingsStore(new AppPathProvider(directory.Path));

        await store.SaveAsync(new AppSettings { Storage = storage });
        var loaded = await store.LoadAsync();

        Assert.Equal(Path.GetFullPath(projectDirectory), loaded.Storage.LastProjectDirectory);
        Assert.Equal(Path.GetFullPath(projectDirectory), ProjectDialogDirectories.GetPreferredDirectory(loaded.Storage, Path.GetTempPath()));
    }
}
