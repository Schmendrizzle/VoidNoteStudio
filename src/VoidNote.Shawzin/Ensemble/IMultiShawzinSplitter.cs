using VoidNote.Domain.Projects;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>Separates normalized notes into stable, explainable creator voices.</summary>
public interface IMultiShawzinSplitter
{
    MultiShawzinSplitResult Split(IReadOnlyList<MidiTrack> sourceTracks, MultiShawzinSplitOptions options);
}
