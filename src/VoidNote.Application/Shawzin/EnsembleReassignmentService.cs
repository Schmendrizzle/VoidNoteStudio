using VoidNote.Application.Commands;
using VoidNote.Domain.Music;
using VoidNote.Shawzin.Ensemble;

namespace VoidNote.Application.Shawzin;

/// <summary>Result of a reversible manual note transfer.</summary>
public sealed record EnsembleReassignmentResult(Guid SourceTrackId, Guid TargetTrackId, IReadOnlyList<Guid> EventIds, int RecalculatedTrackCount);

public interface IEnsembleReassignmentService
{
    EnsembleReassignmentResult MoveNotes(ShawzinEnsemble ensemble, Guid sourceTrackId, Guid targetTrackId, IReadOnlyCollection<Guid> eventIds);
}

/// <summary>Moves one or many normalized notes and re-arranges only the affected tracks.</summary>
public sealed class EnsembleReassignmentService(IUndoRedoService history, IShawzinEnsembleArranger arranger) : IEnsembleReassignmentService
{
    public EnsembleReassignmentResult MoveNotes(ShawzinEnsemble ensemble, Guid sourceTrackId, Guid targetTrackId, IReadOnlyCollection<Guid> eventIds)
    {
        ArgumentNullException.ThrowIfNull(ensemble);
        ArgumentNullException.ThrowIfNull(eventIds);
        if (sourceTrackId == targetTrackId) throw new ArgumentException("Source and target tracks must differ.");
        if (eventIds.Count == 0) throw new ArgumentException("At least one note must be selected.", nameof(eventIds));
        var source = ensemble.Tracks.SingleOrDefault(value => value.Id == sourceTrackId)
            ?? throw new KeyNotFoundException("The source ensemble track was not found.");
        var target = ensemble.Tracks.SingleOrDefault(value => value.Id == targetTrackId)
            ?? throw new KeyNotFoundException("The target ensemble track was not found.");
        var selected = source.SourceTrack.Events.Where(value => eventIds.Contains(value.Id)).ToArray();
        if (selected.Length != eventIds.Distinct().Count()) throw new InvalidOperationException("One or more selected notes are not in the source track.");
        history.Execute(new ReassignNotesCommand(ensemble, source, target, selected, arranger));
        return new EnsembleReassignmentResult(sourceTrackId, targetTrackId, selected.Select(value => value.Id).ToArray(), 2);
    }

    private sealed class ReassignNotesCommand(
        ShawzinEnsemble ensemble,
        ShawzinEnsembleTrack source,
        ShawzinEnsembleTrack target,
        IReadOnlyList<MusicalEvent> notes,
        IShawzinEnsembleArranger arranger) : IUndoableCommand
    {
        public string Description => $"Move {notes.Count} note(s) from {source.DisplayName} to {target.DisplayName}";

        public void Execute() => Apply(source, target);
        public void Undo() => Apply(target, source);

        private void Apply(ShawzinEnsembleTrack from, ShawzinEnsembleTrack to)
        {
            var ids = notes.Select(value => value.Id).ToHashSet();
            if (notes.Any(note => from.SourceTrack.Events.All(value => value.Id != note.Id)))
                throw new InvalidOperationException("The ensemble changed since the reassignment command was created.");
            from.SourceTrack.Events.RemoveAll(value => ids.Contains(value.Id));
            foreach (var note in notes)
                if (to.SourceTrack.Events.All(value => value.Id != note.Id)) to.SourceTrack.Events.Add(note);
            from.SourceTrack.Events.Sort(Compare);
            to.SourceTrack.Events.Sort(Compare);
            arranger.RearrangeTrack(ensemble, from);
            arranger.RearrangeTrack(ensemble, to);
        }

        private static int Compare(MusicalEvent left, MusicalEvent right)
        {
            var time = left.StartTime.Ticks.CompareTo(right.StartTime.Ticks);
            if (time != 0) return time;
            var pitch = left.Pitch.CompareTo(right.Pitch);
            return pitch != 0 ? pitch : left.Id.CompareTo(right.Id);
        }
    }
}
