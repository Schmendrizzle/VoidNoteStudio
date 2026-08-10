using System.IO.Compression;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
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

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsShawzinDomainModel()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "shawzin-roundtrip.vns");
        var eventId = Guid.NewGuid();
        var project = new VoidNoteProject
        {
            Metadata = new ProjectMetadata { Title = "Shawzin Roundtrip" },
            ShawzinTracks =
            [
                new ShawzinTrack
                {
                    Name = "Physical Shawzin track",
                    Scale = ShawzinScale.Phrygian,
                    ShawzinEvents =
                    [
                        new ShawzinEvent(
                            eventId,
                            new AbsoluteTime(1.25m),
                            new ShawzinChord(
                            [
                                new ShawzinNote(ShawzinString.First, ShawzinFret.Sky | ShawzinFret.Water),
                                new ShawzinNote(ShawzinString.Third, ShawzinFret.Sky | ShawzinFret.Water),
                            ])),
                    ],
                },
            ],
        };
        var store = new VnsProjectStore();

        await store.SaveAsync(project, path);
        var loaded = await store.LoadAsync(path);

        var loadedEvent = Assert.Single(Assert.Single(loaded.ShawzinTracks).ShawzinEvents);
        Assert.Equal(eventId, loadedEvent.Id);
        Assert.Equal(ShawzinScale.Phrygian, loaded.ShawzinTracks[0].Scale);
        Assert.Equal(1.25m, loadedEvent.Position.Seconds);
        Assert.Equal([ShawzinString.First, ShawzinString.Third], loadedEvent.Chord.Notes.Select(note => note.String));
        Assert.All(loadedEvent.Chord.Notes, note => Assert.Equal(ShawzinFret.Sky | ShawzinFret.Water, note.Frets));
    }
}
