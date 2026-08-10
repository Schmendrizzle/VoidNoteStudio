using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Projects;

namespace VoidNote.IntegrationTests;

public sealed class ProjectRecoveryTests
{
    [Fact]
    public async Task Autosave_UsesSeparateRecoveryFile_AndDoesNotReplaceOriginal()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "song.vns");
        var project = new VoidNoteProject { Metadata = new() { Title = "Song" } };
        await store.SaveAsync(project, original);
        var originalBytes = await File.ReadAllBytesAsync(original);
        var service = new ProjectRecoveryService(store, new AppPathProvider(directory.Path));

        var candidate = await service.WriteAutosaveAsync(project, original);

        Assert.NotEqual(original, candidate.AutosavePath);
        Assert.True(File.Exists(candidate.AutosavePath));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(original));
    }

    [Fact]
    public async Task Recovery_IsOfferedOnlyWhenNewer_AndDiscardRemovesSnapshot()
    {
        using var directory = new TemporaryDirectory();
        var store = new VnsProjectStore();
        var original = Path.Combine(directory.Path, "song.vns");
        var project = new VoidNoteProject { Metadata = new() { Title = "Recover me" } };
        await store.SaveAsync(project, original);
        var service = new ProjectRecoveryService(store, new AppPathProvider(directory.Path));
        var candidate = await service.WriteAutosaveAsync(project, original);

        var found = Assert.Single(await service.FindRecoverableAsync());
        var recovered = await service.RecoverAsync(found);
        Assert.Equal("Recover me", recovered.Metadata.Title);

        await service.DiscardAsync(found);
        Assert.Empty(await service.FindRecoverableAsync());
        Assert.False(File.Exists(candidate.AutosavePath));
    }
}
