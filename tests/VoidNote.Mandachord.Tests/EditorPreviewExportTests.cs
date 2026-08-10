using VoidNote.Application.Commands;
using VoidNote.Application.Mandachord;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Mandachord.Export;
using VoidNote.Mandachord.Preview;
using VoidNote.Midi;

namespace VoidNote.Mandachord.Tests;

public sealed class EditorPreviewExportTests
{
    [Fact] public void Editor_AddPitchCopyPasteDeleteClear_AllUndoRedo()
    {
        var history = new UndoRedoService(); var editor = new MandachordEditorService(history); var pattern = Pattern();
        editor.SetStep(pattern, MandachordLayer.Melody, 0, 1); var first = Assert.Single(pattern.Steps); Assert.True(history.Undo()); Assert.Empty(pattern.Steps); Assert.True(history.Redo());
        editor.ChangePitch(pattern, [first.Id], 3); Assert.Equal(3, first.PitchPosition); history.Undo(); Assert.Equal(1, first.PitchPosition);
        var copy = editor.Copy(pattern, [first.Id]); editor.Paste(pattern, copy, 4); Assert.Equal(2, pattern.Steps.Count); editor.DeleteSteps(pattern, [first.Id]); Assert.Single(pattern.Steps); history.Undo(); Assert.Equal(2, pattern.Steps.Count); editor.Clear(pattern); Assert.Empty(pattern.Steps); history.Undo(); Assert.Equal(2, pattern.Steps.Count);
    }
    [Fact] public void Editor_PercussionCandidatePatternSectionAndSoundSet_AreUndoable()
    {
        var history = new UndoRedoService(); var editor = new MandachordEditorService(history); var pattern = Pattern(); editor.SetStep(pattern, MandachordLayer.Percussion, 0, percussion: MandachordPercussionCategory.Kick);
        var arrangement = Arrangement(pattern); var project = Project(); editor.AcceptCandidate(project, arrangement); Assert.Single(project.MandachordArrangements); history.Undo(); Assert.Empty(project.MandachordArrangements); history.Redo();
        var second = Pattern("Second"); arrangement.Patterns.Add(second); editor.AssignSection(arrangement, arrangement.Sections[0].Id, second.Id); Assert.Equal(second.Id, arrangement.Sections[0].PatternId); history.Undo();
        var sound = Guid.NewGuid(); editor.ChangeSoundSet(arrangement, sound); Assert.Equal(sound, arrangement.SelectedSoundSetId); history.Undo();
        arrangement.Sections.Clear(); editor.DeletePattern(arrangement, second.Id); Assert.Single(arrangement.Patterns); history.Undo(); Assert.Equal(2, arrangement.Patterns.Count);
    }
    [Fact] public void Preview_IsEightSecondSyntheticWaveAndSoundSetDoesNotChangePattern()
    {
        var pattern = Pattern(); pattern.Steps.Add(new() { Name = "D", Layer = MandachordLayer.Melody, StepIndex = 0, PitchPosition = 0 }); var arrangement = Arrangement(pattern); var json = new VoidNoteMandachordJsonCodec().Export(arrangement);
        var result = new SyntheticMandachordPreviewRenderer().Render(pattern, BuiltInMandachordSoundSets.SyntheticDefault(), 8000);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(result.WaveData, 0, 4)); Assert.Equal(TimeSpan.FromSeconds(8), result.Duration); Assert.Equal(json, new VoidNoteMandachordJsonCodec().Export(arrangement));
    }
    [Fact] public void CombinedPreview_MixesShawzinCompatibleMonoWaveAndMandachordWave()
    {
        var renderer = new SyntheticMandachordPreviewRenderer(); var pattern = Pattern(); pattern.Steps.Add(new() { Name = "kick", Layer = MandachordLayer.Percussion, StepIndex = 0, PercussionCategory = MandachordPercussionCategory.Kick }); var wave = renderer.Render(pattern, BuiltInMandachordSoundSets.SyntheticDefault(), 8000).WaveData;
        var mixed = new PcmCombinedPreviewRenderer().Mix([wave, wave]); Assert.Equal(wave.Length, mixed.Length); Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(mixed, 0, 4));
    }
    [Fact] public async Task MidiExport_HasSeparatedPercussionBassMelodyTracksAndRoundTrips()
    {
        var pattern = Pattern(); pattern.Steps.AddRange([new() { Name = "kick", Layer = MandachordLayer.Percussion, StepIndex = 0, PercussionCategory = MandachordPercussionCategory.Kick }, new() { Name = "bass", Layer = MandachordLayer.Bass, StepIndex = 4, PitchPosition = 0 }, new() { Name = "melody", Layer = MandachordLayer.Melody, StepIndex = 8, PitchPosition = 2 }]);
        await using var stream = new MemoryStream(); await new MandachordMidiExporter(new DryWetMidiFileExporter()).ExportAsync(stream, pattern, ProjectTimeline.CreateDefault()); stream.Position = 0; var result = await new DryWetMidiFileImporter().ImportAsync(stream);
        Assert.Equal(["Mandachord Percussion", "Mandachord Bass", "Mandachord Melody"], result.Tracks.Select(value => value.Name)); Assert.All(result.Tracks, value => Assert.Single(value.Events));
    }
    [Fact] public void InternalJson_RoundTripsAndRejectsClaimedNativeFormat()
    {
        var codec = new VoidNoteMandachordJsonCodec(); var arrangement = Arrangement(Pattern()); var json = codec.Export(arrangement); var loaded = codec.Import(json); Assert.Equal(arrangement.Id, loaded.Id); Assert.Contains("VoidNote Mandachord", json); Assert.Throws<InvalidDataException>(() => codec.Import("{\"Format\":\"Warframe\",\"Version\":1}"));
    }
    private static MandachordPattern Pattern(string name = "Pattern") => new() { Name = name, Section = "Loop", CreatedAt = DateTimeOffset.UnixEpoch, ModifiedAt = DateTimeOffset.UnixEpoch };
    private static MandachordArrangement Arrangement(MandachordPattern pattern) => new() { Name = "Arrangement", SelectedSoundSetId = BuiltInMandachordSoundSets.SyntheticDefault().Id, Patterns = [pattern], Sections = [new() { Name = "Loop", Start = new(0), End = new(15_360), PatternId = pattern.Id }] };
    private static VoidNoteProject Project() => new() { Metadata = new() { Title = "Test" }, MandachordSoundSets = [BuiltInMandachordSoundSets.SyntheticDefault()] };
}
