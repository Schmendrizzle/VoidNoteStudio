using VoidNote.Application.Commands;
using VoidNote.Application.Shawzin;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.IntegrationTests;

public sealed class EnsembleReassignmentTests
{
    [Fact]
    public void MultipleNotes_MoveUndoRedo_AndOnlyAffectedTracksAreRecalculated()
    {
        var ensembleArranger = Arranger();
        var ensemble = ensembleArranger.Arrange(Split(), ProjectTimeline.CreateDefault());
        var source = ensemble.Tracks[0];
        var target = ensemble.Tracks[1];
        var ids = source.SourceTrack.Events.Select(value => value.Id).Take(2).ToArray();
        var sourceBefore = source.SourceTrack.Events.Count;
        var targetBefore = target.SourceTrack.Events.Count;
        var history = new UndoRedoService();
        var service = new EnsembleReassignmentService(history, ensembleArranger);

        var result = service.MoveNotes(ensemble, source.Id, target.Id, ids);
        Assert.Equal(2, result.RecalculatedTrackCount);
        Assert.Equal(sourceBefore - 2, source.SourceTrack.Events.Count);
        Assert.Equal(targetBefore + 2, target.SourceTrack.Events.Count);
        Assert.True(history.Undo());
        Assert.Equal(sourceBefore, source.SourceTrack.Events.Count);
        Assert.Equal(targetBefore, target.SourceTrack.Events.Count);
        Assert.True(history.Redo());
        Assert.Equal(sourceBefore - 2, source.SourceTrack.Events.Count);
        Assert.All(ids, id => Assert.Contains(target.SourceTrack.Events, value => value.Id == id));
    }

    private static MultiShawzinSplitResult Split()
    {
        var notes = Enumerable.Range(0, 8).Select(index => new MusicalEvent(Guid.Parse($"{index + 1:x8}-0000-0000-0000-000000000000"),
            new MusicalTime(index / 2 * 960), new MusicalTime(480), 48 + index % 2 * 7, 100, MusicalEventSource.ImportedMidi, 1m)).ToList();
        var track = new MidiTrack { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Piano", Events = notes };
        return new MultiShawzinSplitter(new VoiceSalienceAnalyzer()).Split([track], new MultiShawzinSplitOptions { ShawzinCount = 2 });
    }

    private static ShawzinEnsembleArranger Arranger()
    {
        var mapper = new ShawzinPitchMapper();
        return new(new ShawzinScaleAnalyzer(), new ShawzinTranspositionAnalyzer(mapper), new ShawzinCompatibilityAnalyzer(mapper), new ShawzinArranger(mapper));
    }
}
