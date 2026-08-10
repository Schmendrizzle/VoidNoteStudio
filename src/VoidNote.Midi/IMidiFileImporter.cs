using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Midi;

/// <summary>Imports a Standard MIDI File into VoidNote's normalized music model.</summary>
public interface IMidiFileImporter
{
    /// <summary>Imports MIDI data from <paramref name="source"/> without taking ownership of the stream.</summary>
    Task<MidiImportResult> ImportAsync(Stream source, CancellationToken cancellationToken = default);
}

/// <summary>Contains normalized tracks and their shared master timeline.</summary>
public sealed record MidiImportResult(ProjectTimeline Timeline, IReadOnlyList<MidiTrack> Tracks);
