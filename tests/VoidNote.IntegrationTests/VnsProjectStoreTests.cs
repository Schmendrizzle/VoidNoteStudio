using System.IO.Compression;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Infrastructure.Projects;
using VoidNote.Domain.Audio;
using System.Text.Json.Nodes;
using System.Text.Json;

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

    [Fact]
    public async Task VersionOneProject_MigratesInMemoryAndCreatesBackupBeforeFirstVersionTwoSave()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "legacy.vns"); var store = new VnsProjectStore();
        await store.SaveAsync(new VoidNoteProject { Metadata = new() { Title = "Legacy" } }, path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("project.json")!; JsonNode manifest;
            await using (var input = entry.Open()) manifest = await JsonNode.ParseAsync(input) ?? throw new InvalidDataException();
            entry.Delete(); var replacement = archive.CreateEntry("project.json");
            manifest["FormatVersion"] = 1; manifest.AsObject().Remove("StemSets"); manifest.AsObject().Remove("AudioTranscriptionReports");
            manifest["Stems"] = new JsonArray(new JsonObject { ["Id"] = Guid.NewGuid(), ["Name"] = "Legacy Stem", ["SourceAudioId"] = null, ["File"] = null });
            await using var output = replacement.Open(); await JsonSerializer.SerializeAsync(output, manifest);
        }

        var loaded = await store.LoadAsync(path);
        Assert.Equal(2, loaded.FormatVersion); Assert.Equal(1, loaded.LoadedFormatVersion); Assert.Empty(loaded.StemSets);
        Assert.Equal("Legacy Stem", Assert.Single(loaded.LegacyStemReferences).Name);
        await store.SaveAsync(loaded, path);
        Assert.True(File.Exists(path + ".v1.bak")); Assert.Equal(2, (await store.LoadAsync(path)).FormatVersion);
    }

    [Fact]
    public async Task StemSetAndTranscriptionProvenance_RoundTripInVersionTwoContainer()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "intelligence.vns");
        var stemPath = Path.Combine(directory.Path, "bass.wav"); await File.WriteAllBytesAsync(stemPath, [1, 2, 3, 4]);
        var originalPath = Path.Combine(directory.Path, "original.wav");
        var original = new AudioSource { Name = "original", SourcePath = originalPath, File = new(originalPath, ProjectPathKind.Absolute), Format = Format() };
        var derived = new AudioSource { Name = "bass", SourcePath = stemPath, ResolvedPath = stemPath, File = new("stems/bass.wav", ProjectPathKind.Embedded), FileSize = 4, LastWriteTimeUtc = File.GetLastWriteTimeUtc(stemPath), Format = Format() };
        var set = new StemSet { Name = "stems", Source = new() { AudioSourceId = original.Id, Duration = new(1m) }, SeparationEngine = "fake", EngineVersion = "1" };
        var stem = new Stem { Name = "Bass", StemSetId = set.Id, SourceAudioId = original.Id, AudioSourceId = derived.Id, Type = StemType.Bass,
            Engine = "fake", EngineVersion = "1", Duration = new(1m), File = derived.File,
            Provenance = new() { SourceAudioId = original.Id, Engine = "fake", EngineVersion = "1", CreatedAt = DateTimeOffset.UtcNow } };
        set.StemTracks.Add(stem);
        var note = new MusicalEvent(Guid.NewGuid(), new(120), new(240), 48, 100, MusicalEventSource.AudioTranscription, 0.91m,
            new() { SourceAudioId = original.Id, SourceStemId = stem.Id, Engine = "fake-midi", EngineVersion = "2", RawConfidence = 0.91m,
                ConfidenceLevel = NoteConfidenceLevel.High, OriginalStart = new(123), OriginalDuration = new(237) });
        var midi = new MidiTrack { Name = "Bass MIDI", Events = [note] };
        var report = new AudioTranscriptionReport { Name = "report", MidiTrackId = midi.Id, Source = set.Source with { StemId = stem.Id }, DetectedNotes = 1, KeptNotes = 1,
            AverageConfidence = 0.91m, HighConfidenceCount = 1, AnalyzedDuration = new(1m), MinimumPitch = 48, MaximumPitch = 48, NoteDensityPerSecond = 1,
            Engine = "fake-midi", EngineVersion = "2", ProcessingDuration = TimeSpan.FromSeconds(1), Settings = new() };
        var project = new VoidNoteProject { Metadata = new() { Title = "AI" }, AudioSources = [original, derived], StemSets = [set], MidiTracks = [midi], AudioTranscriptionReports = [report] };

        var store = new VnsProjectStore(); await store.SaveAsync(project, path); var loaded = await store.LoadAsync(path);

        Assert.Equal(StemType.Bass, Assert.Single(Assert.Single(loaded.StemSets).StemTracks).Type);
        var loadedNote = Assert.Single(Assert.Single(loaded.MidiTracks).Events);
        Assert.Equal(0.91m, loadedNote.AudioProvenance!.RawConfidence); Assert.Equal(new MusicalTime(123), loadedNote.AudioProvenance.OriginalStart);
        Assert.Single(loaded.AudioTranscriptionReports);
    }

    private static AudioFormatInfo Format() => new() { Container = "WAV", Codec = "pcm", SampleRate = 8000, ChannelCount = 1, Duration = new(1m) };
}
