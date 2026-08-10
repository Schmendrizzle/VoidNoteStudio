using VoidNote.Application.Shawzin;
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
        { ShawzinCount = 3, Strategy = MultiShawzinSplitStrategy.CreatorMultitrack });

        Assert.Equal(3, result.Ensemble.Tracks.Count);
        Assert.Equal(9, result.Ensemble.SplitReport.Metrics.SourceNoteCount);
        Assert.Equal(9, result.Ensemble.SplitReport.Metrics.AssignedNoteCount);
        Assert.All(result.Ensemble.Tracks, value => Assert.NotNull(value.ShawzinTrack));
        Assert.True(result.Export.IsValid);
        Assert.Equal(3, result.Export.Tracks.Count(value => !string.IsNullOrEmpty(value.Code)));
        Assert.True(result.Preview.WaveData.Length > 44);

        var repeat = workflow.Create(imported.Tracks, imported.Timeline, new MultiShawzinSplitOptions
        { ShawzinCount = 3, Strategy = MultiShawzinSplitStrategy.CreatorMultitrack });
        Assert.Equal(result.Export.Tracks.Select(value => value.Code), repeat.Export.Tracks.Select(value => value.Code));
    }
}
