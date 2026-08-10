using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Projects;

namespace VoidNote.Mandachord.Generation;

public static class MandachordSourceFactory
{
    public static MandachordSourceTrack FromMidi(VoidNoteProject project, Guid trackId, MandachordLayer? layer = null)
    {
        var track = project.MidiTracks.Single(value => value.Id == trackId);
        var kind = track.Events.Any(value => value.AudioProvenance?.SourceStemId is not null) ? MandachordSourceKind.StemDerivedMidiTrack
            : track.Events.Any(value => value.Source == Domain.Music.MusicalEventSource.AudioTranscription) ? MandachordSourceKind.AudioTranscriptionTrack : MandachordSourceKind.MidiTrack;
        var stemId = track.Events.Select(value => value.AudioProvenance?.SourceStemId).FirstOrDefault(value => value.HasValue);
        return new(track.Id, track.Name, kind, track.Events, layer, stemId);
    }

    public static MandachordSourceTrack FromShawzin(VoidNoteProject project, Guid trackId, MandachordLayer layer = MandachordLayer.Melody)
    {
        var track = project.ShawzinTracks.Single(value => value.Id == trackId);
        if (track.Events.Count == 0) throw new InvalidOperationException("A Shawzin source requires normalized musical Events; physical song-code events alone have no unambiguous pitch.");
        return new(track.Id, track.Name, MandachordSourceKind.ShawzinTrack, track.Events, layer);
    }
}
