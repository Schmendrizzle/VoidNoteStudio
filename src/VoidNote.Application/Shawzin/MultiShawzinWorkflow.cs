using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Shawzin.Preview;

namespace VoidNote.Application.Shawzin;

/// <summary>Complete UI-independent polyphonic MIDI-to-ensemble creator result.</summary>
public sealed record MultiShawzinCreatorResult(
    ShawzinEnsemble Ensemble,
    EnsembleExportReport Export,
    ShawzinPreviewAudio Preview);

public interface IMultiShawzinWorkflow
{
    MultiShawzinCreatorResult Create(IReadOnlyList<MidiTrack> tracks, ProjectTimeline timeline, MultiShawzinSplitOptions options);
    EnsembleExportReport Export(ShawzinEnsemble ensemble);
    ShawzinPreviewAudio Preview(ShawzinEnsemble ensemble);
}

/// <summary>Coordinates splitting, per-track optimization, independent code export and common preview.</summary>
public sealed class MultiShawzinWorkflow(
    IMultiShawzinSplitter splitter,
    IShawzinEnsembleArranger arranger,
    IEnsembleCodeExporter exporter,
    IShawzinEnsemblePreviewRenderer previewRenderer) : IMultiShawzinWorkflow
{
    public MultiShawzinCreatorResult Create(IReadOnlyList<MidiTrack> tracks, ProjectTimeline timeline, MultiShawzinSplitOptions options)
    {
        var split = splitter.Split(tracks, options);
        var ensemble = arranger.Arrange(split, timeline);
        return new MultiShawzinCreatorResult(ensemble, exporter.Export(ensemble), previewRenderer.Render(ensemble));
    }

    public EnsembleExportReport Export(ShawzinEnsemble ensemble) => exporter.Export(ensemble);
    public ShawzinPreviewAudio Preview(ShawzinEnsemble ensemble) => previewRenderer.Render(ensemble);
}
