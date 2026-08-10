using System.Text.Json;
using VoidNote.Application.Projects;
using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Projects;

namespace VoidNote.IntegrationTests;

public sealed class ProjectRecoveryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    [Fact]
    public async Task Autosave_UsesSeparateRecoveryFile_AndDoesNotReplaceOriginal()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "song.vns");
        var project = Project("Song");
        await store.SaveAsync(project, original);
        var originalBytes = await File.ReadAllBytesAsync(original);
        var service = Service(directory, store);

        var candidate = await service.WriteAutosaveAsync(project, original);

        Assert.NotEqual(original, candidate.AutosavePath);
        Assert.True(File.Exists(candidate.AutosavePath));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(original));
    }

    [Fact]
    public async Task Autosave_OlderThanSavedProject_IsNotRecoverable()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "older.vns");
        var project = Project("Older autosave");
        await store.SaveAsync(project, original);
        var candidate = await Service(directory, store).WriteAutosaveAsync(project, original);
        await SetAutosavedAtAsync(candidate, new DateTimeOffset(2026, 8, 10, 21, 47, 7, TimeSpan.Zero));
        File.SetLastWriteTimeUtc(original, new DateTime(2026, 8, 10, 21, 47, 8, DateTimeKind.Utc));

        var found = await Service(directory, store).FindRecoverableAsync([original]);

        Assert.Empty(found);
    }

    [Fact]
    public async Task EqualAbsoluteInstant_WithDifferentOffsets_IsNotRecoverable()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "offset.vns");
        var project = Project("Offset-safe");
        await store.SaveAsync(project, original);
        var candidate = await Service(directory, store).WriteAutosaveAsync(project, original);
        var instantUtc = new DateTimeOffset(2026, 8, 10, 21, 47, 7, TimeSpan.Zero);
        await SetAutosavedAtAsync(candidate, instantUtc.ToOffset(TimeSpan.FromHours(2)));
        File.SetLastWriteTimeUtc(original, instantUtc.UtcDateTime);

        var found = await Service(directory, store).FindRecoverableAsync([original]);

        Assert.Empty(found);
    }

    [Fact]
    public async Task Autosave_ActuallyNewer_IsRecoverable()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "newer.vns");
        var project = Project("Recover me");
        await store.SaveAsync(project, original);
        var candidate = await Service(directory, store).WriteAutosaveAsync(project, original);
        File.SetLastWriteTimeUtc(original, new DateTime(2026, 8, 10, 21, 47, 7, DateTimeKind.Utc));
        await SetAutosavedAtAsync(candidate, new DateTimeOffset(2026, 8, 10, 21, 47, 8, TimeSpan.Zero));

        var found = Assert.Single(await Service(directory, store).FindRecoverableAsync([original]));

        Assert.Equal(project.Id, found.ProjectId);
        Assert.Equal(Path.GetFullPath(original), found.OriginalProjectPath);
    }

    [Fact]
    public async Task CleanSave_AndCleanShutdown_LeavesNoRecoveryForRestart()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "clean.vns");
        var project = Project("Clean");
        var runningService = Service(directory, store);
        var candidate = await runningService.WriteAutosaveAsync(project, null);

        await store.SaveAsync(project, original);
        await runningService.CompleteProjectSaveAsync(project.Id, null, original);
        await runningService.CompleteCleanShutdownAsync(project.Id, original, hasUnsavedChanges: false);

        Assert.Empty(await Service(directory, store).FindRecoverableAsync([original]));
        Assert.False(File.Exists(candidate.AutosavePath));
    }

    [Fact]
    public async Task UncleanShutdown_WithNewerAutosave_IsRecoverableAfterRestart()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "crashed.vns");
        var project = Project("Crashed");
        await store.SaveAsync(project, original);
        File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddMinutes(-2));
        await Service(directory, store).WriteAutosaveAsync(project, original);

        var found = Assert.Single(await Service(directory, store).FindRecoverableAsync([original]));

        Assert.Equal(project.Id, found.ProjectId);
    }

    [Fact]
    public async Task Recoveries_ForTwoProjects_AreMappedToTheirOwnProjects()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var firstPath = Path.Combine(directory.Path, "first.vns");
        var secondPath = Path.Combine(directory.Path, "second.vns");
        var first = Project("First");
        var second = Project("Second");
        await store.SaveAsync(first, firstPath);
        await store.SaveAsync(second, secondPath);
        File.SetLastWriteTimeUtc(firstPath, DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(secondPath, DateTime.UtcNow.AddMinutes(-2));
        var service = Service(directory, store);
        await service.WriteAutosaveAsync(first, firstPath);
        await service.WriteAutosaveAsync(second, secondPath);

        var found = await Service(directory, store).FindRecoverableAsync([secondPath, firstPath]);

        Assert.Equal(2, found.Count);
        Assert.Equal(Path.GetFullPath(firstPath), found.Single(value => value.ProjectId == first.Id).OriginalProjectPath);
        Assert.Equal(Path.GetFullPath(secondPath), found.Single(value => value.ProjectId == second.Id).OriginalProjectPath);
    }

    [Fact]
    public async Task SaveAs_RemovesSupersededAutosave_AndFutureRecoveryUsesNewPath()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var oldPath = Path.Combine(directory.Path, "old.vns");
        var newPath = Path.Combine(directory.Path, "new.vns");
        var project = Project("Save As");
        await store.SaveAsync(project, oldPath);
        var service = Service(directory, store);
        var obsolete = await service.WriteAutosaveAsync(project, oldPath);

        await store.SaveAsync(project, newPath);
        await service.CompleteProjectSaveAsync(project.Id, oldPath, newPath);

        Assert.False(File.Exists(obsolete.AutosavePath));
        File.SetLastWriteTimeUtc(newPath, DateTime.UtcNow.AddMinutes(-2));
        await service.WriteAutosaveAsync(project, newPath);
        var found = Assert.Single(await Service(directory, store).FindRecoverableAsync([oldPath, newPath]));
        Assert.Equal(Path.GetFullPath(newPath), found.OriginalProjectPath);
    }

    [Fact]
    public async Task LegacyUnassociatedRecovery_IsSafelyMatchedByProjectId_AndCleanedWhenObsolete()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var savedPath = Path.Combine(directory.Path, "saved-after-autosave.vns");
        var project = Project("Legacy");
        var candidate = await Service(directory, store).WriteAutosaveAsync(project, null);
        await SetAutosavedAtAsync(candidate, new DateTimeOffset(2026, 8, 10, 21, 47, 7, TimeSpan.Zero));
        await store.SaveAsync(project, savedPath);
        File.SetLastWriteTimeUtc(savedPath, new DateTime(2026, 8, 10, 21, 47, 8, DateTimeKind.Utc));

        var found = await Service(directory, store).FindRecoverableAsync([savedPath]);

        Assert.Empty(found);
        Assert.False(File.Exists(candidate.AutosavePath));
        Assert.False(File.Exists(MetadataPath(candidate)));
    }

    [Fact]
    public async Task Recovery_PathContainingDifferentProject_IsNotFalselyAssociated()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var firstPath = Path.Combine(directory.Path, "first.vns");
        var secondPath = Path.Combine(directory.Path, "second.vns");
        var first = Project("First");
        var second = Project("Second");
        await store.SaveAsync(first, firstPath);
        await store.SaveAsync(second, secondPath);
        File.SetLastWriteTimeUtc(secondPath, DateTime.UtcNow.AddMinutes(-2));
        var candidate = await Service(directory, store).WriteAutosaveAsync(first, firstPath);
        await WriteCandidateAsync(candidate with { OriginalProjectPath = secondPath });

        var found = Assert.Single(await Service(directory, store).FindRecoverableAsync([firstPath, secondPath]));

        Assert.Equal(first.Id, found.ProjectId);
        Assert.Equal(Path.GetFullPath(firstPath), found.OriginalProjectPath);
        Assert.NotEqual(Path.GetFullPath(secondPath), found.OriginalProjectPath);
    }

    [Fact]
    public async Task Discard_RemovesOnlyRecoverySnapshot_AndNeverOriginal()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "keep-original.vns");
        var project = Project("Discard");
        await store.SaveAsync(project, original);
        File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddMinutes(-2));
        var service = Service(directory, store);
        await service.WriteAutosaveAsync(project, original);
        var found = Assert.Single(await service.FindRecoverableAsync([original]));

        await service.DiscardAsync(found);

        Assert.True(File.Exists(original));
        Assert.Empty(await service.FindRecoverableAsync([original]));
    }

    private static VoidNoteProject Project(string title) => new() { Metadata = new() { Title = title } };
    private static ProjectRecoveryService Service(TemporaryDirectory directory, VnsProjectStore store) => new(store, new AppPathProvider(directory.Path));
    private static string MetadataPath(RecoveryCandidate candidate) => Path.ChangeExtension(candidate.AutosavePath, ".recovery.json");

    private static Task SetAutosavedAtAsync(RecoveryCandidate candidate, DateTimeOffset autosavedAt) =>
        WriteCandidateAsync(candidate with { AutosavedAtUtc = autosavedAt });

    private static Task WriteCandidateAsync(RecoveryCandidate candidate) =>
        File.WriteAllTextAsync(MetadataPath(candidate), JsonSerializer.Serialize(candidate, JsonOptions));
}
