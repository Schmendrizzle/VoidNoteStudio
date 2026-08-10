using VoidNote.Application.Commands;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Projects;

namespace VoidNote.Application.Creator;

public sealed class CreatorEditService(IUndoRedoService history)
{
    public void AddSession(VoidNoteProject project, CreatorSession session) => history.Execute(new ListCommand<CreatorSession>("Create creator session", project.CreatorSessions, session, true));
    public void RemoveSession(VoidNoteProject project, CreatorSession session) => history.Execute(new ListCommand<CreatorSession>("Delete creator session", project.CreatorSessions, session, false));
    public void AddTake(CreatorSession session, CreatorTake take) => history.Execute(new ListCommand<CreatorTake>("Add creator take", session.Takes, take, true));
    public void RemoveTake(CreatorSession session, CreatorTake take) => history.Execute(new ListCommand<CreatorTake>("Remove creator take", session.Takes, take, false));
    public void AddSection(CreatorSession session, CreatorSection section) => history.Execute(new ListCommand<CreatorSection>("Add creator section", session.Sections, section, true));
    public void RemoveSection(CreatorSession session, CreatorSection section) => history.Execute(new ListCommand<CreatorSection>("Remove creator section", session.Sections, section, false));
    public void SetStatus(CreatorTake take, CreatorTakeStatus status, DateTimeOffset now, string reason = "")
    {
        var old = take.Status; history.Execute(new DelegateCommand("Change take status", () => take.ChangeStatus(status, now, reason), () =>
        { take.Status = old; if (take.StatusHistory.Count > 0) take.StatusHistory.RemoveAt(take.StatusHistory.Count - 1); }));
    }
    public void SetNotes(CreatorTake take, string notes) { var old = take.Notes; history.Execute(new DelegateCommand("Edit take notes", () => take.Notes = notes, () => take.Notes = old)); }
    public void AssignTrack(CreatorTake take, Guid trackId, string name) { var oldId = take.SourceTrackId; var oldName = take.SourceName; history.Execute(new DelegateCommand("Assign creator track", () => { take.SourceTrackId = trackId; take.SourceName = name; }, () => { take.SourceTrackId = oldId; take.SourceName = oldName; })); }

    private sealed record DelegateCommand(string Description, Action Apply, Action Revert) : IUndoableCommand { public void Execute() => Apply(); public void Undo() => Revert(); }
    private sealed class ListCommand<T>(string description, IList<T> list, T item, bool adding) : IUndoableCommand
    {
        private int _index;
        public string Description => description;
        public void Execute() { if (adding) list.Add(item); else { _index = list.IndexOf(item); list.Remove(item); } }
        public void Undo() { if (adding) list.Remove(item); else list.Insert(Math.Max(0, _index), item); }
    }
}
