using System.IO.Compression;
using System.Text.Json.Nodes;
using VoidNote.Application.Creator;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Projects;
using VoidNote.Mandachord.Generation;

namespace VoidNote.Mandachord.Tests;

public sealed class PersistenceCreatorIntegrationTests
{
    [Fact] public async Task VnsPersistence_RoundTripsArrangementPatternsProvenanceAndSoundSet()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.vns"); try { var project = Project(); var store = new VnsProjectStore(); await store.SaveAsync(project, path); var loaded = await store.LoadAsync(path); var step = Assert.Single(Assert.Single(loaded.MandachordArrangements).Patterns[0].Steps); Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000099"), step.Provenance.SourceEventId); Assert.Single(loaded.MandachordSoundSets); }
        finally { File.Delete(path); }
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Migration_V1V2V3OpensAndCreatesVersionedBackup(int version)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.vns"); try { var store = new VnsProjectStore(); await store.SaveAsync(Project(), path); using (var archive = ZipFile.Open(path, ZipArchiveMode.Update)) { var entry = archive.GetEntry("project.json")!; JsonNode root; using (var reader = new StreamReader(entry.Open())) root = JsonNode.Parse(await reader.ReadToEndAsync())!; entry.Delete(); root["FormatVersion"] = version; root.AsObject().Remove("MandachordArrangements"); root.AsObject().Remove("MandachordSoundSets"); if (version <= 2) root.AsObject().Remove("CreatorSessions"); var replacement = archive.CreateEntry("project.json"); await using var writer = new StreamWriter(replacement.Open()); await writer.WriteAsync(root.ToJsonString()); }
            var loaded = await store.LoadAsync(path); Assert.Equal(version, loaded.LoadedFormatVersion); Assert.Empty(loaded.MandachordArrangements); await store.SaveAsync(loaded, path); Assert.True(File.Exists(path + $".v{version}.bak")); }
        finally { File.Delete(path); foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".v*.bak")) File.Delete(file); }
    }
    [Fact] public void CreatorFactory_CreatesManualMandachordTakeWithMetadataCountInAndSync()
    {
        var project = Project(); var factory = new CreatorSessionFactory(new CreatorTimingService()); var session = factory.FromProject(project); var take = Assert.Single(session.Takes);
        Assert.Equal(CreatorSourceType.Mandachord, take.SourceType); Assert.Equal(project.MandachordArrangements[0].Id, take.MandachordArrangementId); Assert.False(take.RequiresGameBridge); Assert.NotNull(take.SyncMetadata); Assert.Equal("Faithful", take.MandachordPreset);
    }
    [Fact] public void AudioTranscriptionAndStemProvenance_FlowsToMandachordWithoutSecondEngine()
    {
        var project = Project(includeArrangement: false); var stemId = Guid.NewGuid(); var midi = new MidiTrack { Name = "Bass stem MIDI", Events = [new(Guid.NewGuid(), new(0), new(480), 38, 100, MusicalEventSource.AudioTranscription, 0.9m, new() { SourceAudioId = Guid.NewGuid(), SourceStemId = stemId, Engine = "fake", EngineVersion = "1", RawConfidence = 0.9m, ConfidenceLevel = NoteConfidenceLevel.High, OriginalStart = new(0), OriginalDuration = new(480) })] }; project.MidiTracks.Add(midi);
        var source = MandachordSourceFactory.FromMidi(project, midi.Id, MandachordLayer.Bass); Assert.Equal(MandachordSourceKind.StemDerivedMidiTrack, source.Kind); Assert.Equal(stemId, source.SourceStemId);
    }
    [Fact] public void CombinedProject_HoldsShawzinMandachordAndCreatorOnOneTimeline()
    {
        var project = Project(); project.ShawzinTracks.Add(new() { Name = "Shawzin" }); var session = new CreatorSessionFactory(new CreatorTimingService()).FromProject(project);
        Assert.Same(project.Timeline, session.MasterTimeline); Assert.Single(project.ShawzinTracks); Assert.Single(project.MandachordArrangements); Assert.Contains(session.Takes, value => value.SourceType == CreatorSourceType.Mandachord);
    }
    private static VoidNoteProject Project(bool includeArrangement = true)
    {
        var sound = BuiltInMandachordSoundSets.SyntheticDefault(); var pattern = new MandachordPattern { Name = "Intro", Section = "Intro", Preset = MandachordGenerationPreset.Faithful, CreatedAt = DateTimeOffset.UnixEpoch, ModifiedAt = DateTimeOffset.UnixEpoch, Steps = [new() { Name = "D", Layer = MandachordLayer.Melody, StepIndex = 0, PitchPosition = 0, Provenance = new() { SourceEventId = Guid.Parse("00000000-0000-0000-0000-000000000099"), GeneratorVersion = "1", Preset = MandachordGenerationPreset.Faithful } }] };
        var arrangement = new MandachordArrangement { Name = "Mandachord", Preset = MandachordGenerationPreset.Faithful, SelectedSoundSetId = sound.Id, Patterns = [pattern], Sections = [new() { Name = "Intro", Start = new(0), End = new(15_360), PatternId = pattern.Id }] };
        return new() { Metadata = new() { Title = "Combined" }, MandachordSoundSets = [sound], MandachordArrangements = includeArrangement ? [arrangement] : [] };
    }
}
