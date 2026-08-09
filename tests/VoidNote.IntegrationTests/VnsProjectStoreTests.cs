using System.IO.Compression;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Projects;

namespace VoidNote.IntegrationTests;

public sealed class VnsProjectStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsNormalizedProject()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "roundtrip.vns");
        var eventId = Guid.NewGuid();
        var project = new VoidNoteProject
        {
            Metadata = new ProjectMetadata { Title = "Roundtrip" },
            MidiTracks =
            [
                new MidiTrack
                {
                    Name = "Foundation track",
                    Events =
                    [
                        new MusicalEvent(eventId, new MusicalTime(960), new MusicalTime(480), 60, 90,
                            MusicalEventSource.Manual, 1m),
                    ],
                },
            ],
        };
        var store = new VnsProjectStore();

        await store.SaveAsync(project, path);
        var loaded = await store.LoadAsync(path);

        Assert.Equal("Roundtrip", loaded.Metadata.Title);
        Assert.Equal(eventId, Assert.Single(Assert.Single(loaded.MidiTracks).Events).Id);
        using var archive = ZipFile.OpenRead(path);
        Assert.NotNull(archive.GetEntry("project.json"));
    }

    [Fact]
    public async Task SaveAsync_RejectsWrongExtensionWithoutWriting()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "project.zip");
        var store = new VnsProjectStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new VoidNoteProject(), path));
        Assert.False(File.Exists(path));
    }
}
