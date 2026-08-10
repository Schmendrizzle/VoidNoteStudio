using VoidNote.Application.Commands;
using VoidNote.Application.Creator;
using VoidNote.Application.Mandachord;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Projects;
using VoidNote.Mandachord.Generation;
using VoidNote.Mandachord.Mapping;
using VoidNote.Mandachord.Preview;
using VoidNote.Shawzin.Ensemble;

namespace VoidNote.IntegrationTests;

public sealed class MandachordEndToEndTests
{
    [Fact] public async Task FlowA_MidiGenerateEditPreviewPersistReload()
    {
        var project = Project(); var midi = Midi("Lead", MusicalEventSource.ImportedMidi); project.MidiTracks.Add(midi); var generator = Generator(); var candidate = generator.Generate(project.Timeline, [new(midi.Id, midi.Name, MandachordSourceKind.MidiTrack, midi.Events)], MandachordGenerationPreset.Faithful, new()).Candidates.First();
        var history = new UndoRedoService(); var editor = new MandachordEditorService(history); editor.ChangePitch(candidate.Arrangement.Patterns[0], [candidate.Arrangement.Patterns[0].Steps.First(value => value.Layer == MandachordLayer.Melody).Id], 2); editor.AcceptCandidate(project, candidate.Arrangement);
        var preview = new SyntheticMandachordPreviewRenderer().Render(candidate.Arrangement.Patterns[0], project.MandachordSoundSets[0]); Assert.True(preview.WaveData.Length > 44);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.vns"); try { await new VnsProjectStore().SaveAsync(project, path); var loaded = await new VnsProjectStore().LoadAsync(path); Assert.Single(loaded.MandachordArrangements); Assert.Contains(loaded.MandachordArrangements[0].Patterns[0].Steps, value => value.Provenance.EditKind == MandachordStepEditKind.ManualModified); } finally { File.Delete(path); }
    }
    [Fact] public void FlowB_AudioStemDiagnosticTranscriptionToMandachordAndCreatorTake()
    {
        var project = Project(); var midi = Midi("Fake Bass Stem", MusicalEventSource.AudioTranscription, stem: Guid.Parse("00000000-0000-0000-0000-000000000777")); project.MidiTracks.Add(midi); var source = MandachordSourceFactory.FromMidi(project, midi.Id, MandachordLayer.Bass); var result = Generator().Generate(project.Timeline, [source], MandachordGenerationPreset.Faithful, new()); project.MandachordArrangements.Add(result.Candidates[0].Arrangement);
        var take = new CreatorSessionFactory(new CreatorTimingService()).FromProject(project).Takes.Single(value => value.SourceType == CreatorSourceType.Mandachord); Assert.False(take.RequiresGameBridge); Assert.NotNull(take.SyncMetadata); Assert.Equal(MandachordSourceKind.StemDerivedMidiTrack, source.Kind);
    }
    [Fact] public void FlowC_PolyphonicMidiMultiShawzinPlusMandachordCombinedProjectAndCreatorSession()
    {
        var project = Project(); var midi = Midi("Polyphonic", MusicalEventSource.ImportedMidi, polyphonic: true); project.MidiTracks.Add(midi); var split = new MultiShawzinSplitter(new VoiceSalienceAnalyzer()).Split([midi], new() { ShawzinCount = 2, Strategy = MultiShawzinSplitStrategy.FullEnsemble });
        foreach (var voice in split.Voices) project.ShawzinTracks.Add(new() { Name = voice.DisplayName, Events = [.. voice.SourceTrack.Events] });
        project.MandachordArrangements.Add(Generator().Generate(project.Timeline, [new(midi.Id, midi.Name, MandachordSourceKind.MidiTrack, midi.Events)], MandachordGenerationPreset.Recognizable, new()).Candidates[0].Arrangement);
        var session = new CreatorSessionFactory(new CreatorTimingService()).FromProject(project); Assert.Equal(2, project.ShawzinTracks.Count); Assert.Single(project.MandachordArrangements); Assert.Same(project.Timeline, session.MasterTimeline); Assert.Contains(session.Takes, value => value.SourceType == CreatorSourceType.Mandachord);
    }
    private static MandachordGenerator Generator() => new(new MandachordPitchMapper(), new MandachordTimingMapper());
    private static VoidNoteProject Project() => new() { Metadata = new() { Title = "E2E" }, MandachordSoundSets = [BuiltInMandachordSoundSets.SyntheticDefault()] };
    private static MidiTrack Midi(string name, MusicalEventSource source, Guid? stem = null, bool polyphonic = false)
    {
        var events = Enumerable.Range(0, 12).Select(i => new MusicalEvent(Id(i, 62), new(i * 240), new(240), 62 + new[] { 0, 3, 5, 7, 10 }[i % 5], 100, source, 0.9m, stem.HasValue ? new() { SourceAudioId = Guid.Parse("00000000-0000-0000-0000-000000000778"), SourceStemId = stem, Engine = "diagnostic-fake", EngineVersion = "1", RawConfidence = 0.9m, ConfidenceLevel = NoteConfidenceLevel.High, OriginalStart = new(i * 240), OriginalDuration = new(240) } : null)).ToList();
        if (polyphonic) events.AddRange(Enumerable.Range(0, 12).Select(i => new MusicalEvent(Id(i, 38), new(i * 240), new(480), 38, 90, source, 1m)));
        return new() { Name = name, Events = events };
    }
    private static Guid Id(int index, int pitch) { Span<byte> bytes = stackalloc byte[16]; BitConverter.TryWriteBytes(bytes, index + 1); BitConverter.TryWriteBytes(bytes[8..], pitch); return new(bytes); }
}
