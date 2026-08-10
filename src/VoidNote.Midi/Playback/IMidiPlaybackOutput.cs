namespace VoidNote.Midi.Playback;

/// <summary>Receives library-independent MIDI playback events.</summary>
public interface IMidiPlaybackOutput
{
    /// <summary>Sends one due playback event.</summary>
    ValueTask SendAsync(ScheduledMidiEvent midiEvent, CancellationToken cancellationToken);

    /// <summary>Stops sounding notes after pause, stop, seek, or cancellation.</summary>
    ValueTask AllNotesOffAsync(CancellationToken cancellationToken);
}
