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
    public async Task VersionOneProject_MigratesInMemoryAndCreatesBackupBeforeFirstCurrentVersionSave()
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
        Assert.Equal(VoidNoteProject.CurrentFormatVersion, loaded.FormatVersion); Assert.Equal(1, loaded.LoadedFormatVersion); Assert.Empty(loaded.StemSets);
        Assert.Equal("Legacy Stem", Assert.Single(loaded.LegacyStemReferences).Name);
        await store.SaveAsync(loaded, path);
        Assert.True(File.Exists(path + ".v1.bak")); Assert.Equal(VoidNoteProject.CurrentFormatVersion, (await store.LoadAsync(path)).FormatVersion);
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

    [Theory]
    [InlineData("../escape.wav")]
    [InlineData("/absolute.wav")]
    [InlineData("drive:/absolute.wav")]
    public async Task LoadAsync_RejectsUnsafeEmbeddedArchivePaths(string unsafePath)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "unsafe.vns");
        var project = new VoidNoteProject
        {
            Metadata = new() { Title = "Unsafe" },
            AudioSources =
            [
                new AudioSource
                {
                    Name = "unsafe", SourcePath = "unsafe.wav", File = new("audio/safe.wav", ProjectPathKind.Embedded),
                    FileSize = 1, Format = Format(),
                },
            ],
        };
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var manifest = archive.CreateEntry("project.json");
            var document = JsonSerializer.SerializeToNode(project)!.AsObject();
            document["AudioSources"]![0]!["File"]!["Path"] = unsafePath;
            await using (var output = manifest.Open()) await JsonSerializer.SerializeAsync(output, document);
            var asset = archive.CreateEntry(unsafePath);
            await using var outputAsset = asset.Open(); await outputAsset.WriteAsync(new byte[] { 1 });
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => new VnsProjectStore().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_RejectsDuplicateManifestEntries()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "duplicate.vns");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var _ in Enumerable.Range(0, 2))
            {
                var entry = archive.CreateEntry("project.json");
                await using var output = entry.Open();
                await JsonSerializer.SerializeAsync(output, new VoidNoteProject());
            }
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => new VnsProjectStore().LoadAsync(path));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OlderProjectVersions_MigrateToVersionFourWithoutDataLoss(int version)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, $"version-{version}.vns");
        var store = new VnsProjectStore();
        var noteId = Guid.NewGuid();
        await store.SaveAsync(new VoidNoteProject
        {
            Metadata = new() { Title = $"Version {version}" },
            MidiTracks = [new MidiTrack { Name = "Keep", Events = [new(noteId, new(10), new(20), 64, 100, MusicalEventSource.Manual, 1)] }],
        }, path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("project.json")!; JsonNode manifest;
            await using (var input = entry.Open()) manifest = await JsonNode.ParseAsync(input) ?? throw new InvalidDataException();
            entry.Delete(); manifest["FormatVersion"] = version;
            if (version == 2) manifest.AsObject().Remove("CreatorSessions");
            manifest.AsObject().Remove("MandachordArrangements"); manifest.AsObject().Remove("MandachordSoundSets");
            var replacement = archive.CreateEntry("project.json"); await using var output = replacement.Open(); await JsonSerializer.SerializeAsync(output, manifest);
        }

        var loaded = await store.LoadAsync(path);

        Assert.Equal(version, loaded.LoadedFormatVersion);
        Assert.Equal(noteId, Assert.Single(Assert.Single(loaded.MidiTracks).Events).Id);
        await store.SaveAsync(loaded, path);
        Assert.True(File.Exists(path + $".v{version}.bak"));
    }

    [Fact]
    public async Task VersionFourStressProject_RoundTripsManyTracksAndEvents()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "stress.vns");
        var tracks = Enumerable.Range(0, 80).Select(track => new MidiTrack
        {
            Name = $"Track {track}",
            Events = Enumerable.Range(0, 125).Select(note => new MusicalEvent(Guid.NewGuid(), new(note * 120L), new(100), 36 + note % 60, 80, MusicalEventSource.Generated, 1)).ToList(),
        }).ToList();
        var project = new VoidNoteProject { Metadata = new() { Title = "Stress" }, MidiTracks = tracks };

        var store = new VnsProjectStore(); await store.SaveAsync(project, path); var loaded = await store.LoadAsync(path);

        Assert.Equal(80, loaded.MidiTracks.Count);
        Assert.Equal(10_000, loaded.MidiTracks.Sum(value => value.Events.Count));
        Assert.All(loaded.MidiTracks, value => Assert.Equal(125, value.Events.Count));
    }

    private static AudioFormatInfo Format() => new() { Container = "WAV", Codec = "pcm", SampleRate = 8000, ChannelCount = 1, Duration = new(1m) };
}
