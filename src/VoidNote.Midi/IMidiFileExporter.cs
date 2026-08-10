using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Midi;

/// <summary>Exports normalized VoidNote tracks as a Standard MIDI File.</summary>
public interface IMidiFileExporter
{
    /// <summary>Writes a format-1 Standard MIDI File without taking ownership of the stream.</summary>
    Task ExportAsync(
        Stream destination,
        ProjectTimeline timeline,
        IReadOnlyList<MidiTrack> tracks,
        CancellationToken cancellationToken = default);
}
