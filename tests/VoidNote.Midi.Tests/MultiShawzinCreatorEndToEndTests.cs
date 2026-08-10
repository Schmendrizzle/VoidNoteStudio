using VoidNote.Application.Shawzin;
using VoidNote.Application.Creator;
using VoidNote.Domain.Projects;
using VoidNote.Midi.Tests.Fixtures;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Midi.Tests;

public sealed class MultiShawzinCreatorEndToEndTests
{
    [Fact]
    public async Task PolyphonicMidi_ImportSplitArrangeExportAndPreview_CompletesOffline()
    {
        await using var midi = MidiFixtureFactory.PolyphonicCreatorFlow();
        var imported = await new DryWetMidiFileImporter().ImportAsync(midi);
        var mapper = new ShawzinPitchMapper();
        var arranger = new ShawzinEnsembleArranger(new ShawzinScaleAnalyzer(), new ShawzinTranspositionAnalyzer(mapper),
            new ShawzinCompatibilityAnalyzer(mapper), new ShawzinArranger(mapper));
        var workflow = new MultiShawzinWorkflow(new MultiShawzinSplitter(new VoiceSalienceAnalyzer()), arranger,
            new EnsembleCodeExporter(new WarframeShawzinCodeEncoder()), new SyntheticShawzinEnsemblePreviewRenderer());

        var result = workflow.Create(imported.Tracks, imported.Timeline, new MultiShawzinSplitOptions
        { ShawzinCount = 4, Strategy = MultiShawzinSplitStrategy.CreatorMultitrack });

        Assert.Equal(4, result.Ensemble.Tracks.Count);
        Assert.Equal(12, result.Ensemble.SplitReport.Metrics.SourceNoteCount);
        Assert.Equal(12, result.Ensemble.SplitReport.Metrics.AssignedNoteCount);
        Assert.All(result.Ensemble.Tracks, value => Assert.NotNull(value.ShawzinTrack));
        Assert.True(result.Export.IsValid);
        Assert.Equal(4, result.Export.Tracks.Count(value => !string.IsNullOrEmpty(value.Code)));
        Assert.True(result.Preview.WaveData.Length > 44);

        var project = new VoidNoteProject { Metadata = new() { Title = "Polyphonic Creator" }, Timeline = imported.Timeline };
        var timing = new CreatorTimingService();
        var session = new CreatorSessionFactory(timing).FromEnsemble(project, result.Ensemble, result.Export);
        Assert.Equal(4, session.Takes.Count);
        Assert.Single(session.Takes.Select(value => timing.Plan(session, value).Markers.MusicStart).Distinct());
        Assert.All(session.Takes, value => { Assert.False(string.IsNullOrWhiteSpace(value.SongCode)); Assert.True(value.RequiresGameBridge); });
        var sync = new CreatorExportService(timing).ExportJson(session, 30);
        Assert.Contains("MusicStartFrame", sync);

        var repeat = workflow.Create(imported.Tracks, imported.Timeline, new MultiShawzinSplitOptions
        { ShawzinCount = 4, Strategy = MultiShawzinSplitStrategy.CreatorMultitrack });
        Assert.Equal(result.Export.Tracks.Select(value => value.Code), repeat.Export.Tracks.Select(value => value.Code));
    }
}
