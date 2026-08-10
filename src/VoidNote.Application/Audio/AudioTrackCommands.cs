using VoidNote.Application.Commands;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Projects;

namespace VoidNote.Application.Audio;

public sealed class RemoveAudioTrackCommand(VoidNoteProject project, AudioTrack track) : IUndoableCommand
{
    private int _index = -1;
    public string Description => $"Remove audio track '{track.Name}'";
    public void Execute() { _index = project.AudioTracks.IndexOf(track); if (_index >= 0) project.AudioTracks.RemoveAt(_index); }
    public void Undo() { if (_index >= 0 && !project.AudioTracks.Contains(track)) project.AudioTracks.Insert(_index, track); }
}

public sealed class SetAudioTrackValueCommand<T>(string description, Action<T> setter, T before, T after) : IUndoableCommand
{
    public string Description { get; } = description;
    public void Execute() => setter(after);
    public void Undo() => setter(before);
}
