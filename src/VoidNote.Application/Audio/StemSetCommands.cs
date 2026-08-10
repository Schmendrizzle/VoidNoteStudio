using VoidNote.Application.Commands;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Projects;

namespace VoidNote.Application.Audio;

/// <summary>Removes derived stem metadata/tracks/sources without deleting original or local files and supports undo.</summary>
public sealed class RemoveStemSetCommand(VoidNoteProject project, StemSet stemSet) : IUndoableCommand
{
    private int _setIndex;
    private readonly List<(int Index, AudioSource Source)> _sources = [];
    private readonly List<(int Index, AudioTrack Track)> _tracks = [];
    public string Description => $"Remove stem set '{stemSet.Name}'";

    public void Execute()
    {
        _setIndex = project.StemSets.IndexOf(stemSet);
        var sourceIds = stemSet.StemTracks.Select(value => value.AudioSourceId).ToHashSet();
        _sources.Clear(); _sources.AddRange(project.AudioSources.Select((value, index) => (index, value)).Where(value => sourceIds.Contains(value.value.Id)).Select(value => (value.index, value.value)));
        _tracks.Clear(); _tracks.AddRange(project.AudioTracks.Select((value, index) => (index, value)).Where(value => value.value.Clips.Any(clip => sourceIds.Contains(clip.SourceId))).Select(value => (value.index, value.value)));
        if (_setIndex >= 0) project.StemSets.RemoveAt(_setIndex);
        foreach (var item in _tracks.OrderByDescending(value => value.Index)) project.AudioTracks.Remove(item.Track);
        foreach (var item in _sources.OrderByDescending(value => value.Index)) project.AudioSources.Remove(item.Source);
    }

    public void Undo()
    {
        foreach (var item in _sources.OrderBy(value => value.Index)) project.AudioSources.Insert(Math.Min(item.Index, project.AudioSources.Count), item.Source);
        foreach (var item in _tracks.OrderBy(value => value.Index)) project.AudioTracks.Insert(Math.Min(item.Index, project.AudioTracks.Count), item.Track);
        if (!project.StemSets.Contains(stemSet)) project.StemSets.Insert(Math.Min(_setIndex, project.StemSets.Count), stemSet);
    }
}
