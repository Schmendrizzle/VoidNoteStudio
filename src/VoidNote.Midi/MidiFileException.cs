namespace VoidNote.Midi;

/// <summary>Reports a MIDI file that cannot be represented by the VoidNote MIDI core.</summary>
public sealed class MidiFileException : Exception
{
    /// <summary>Creates a MIDI file exception.</summary>
    public MidiFileException(string message) : base(message)
    {
    }

    /// <summary>Creates a MIDI file exception with its underlying cause.</summary>
    public MidiFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
