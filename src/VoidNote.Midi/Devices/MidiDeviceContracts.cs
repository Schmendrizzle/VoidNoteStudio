using VoidNote.Midi.Playback;

namespace VoidNote.Midi.Devices;

/// <summary>Describes a MIDI endpoint without exposing a vendor-library type.</summary>
public sealed record MidiDeviceDescriptor(string Id, string Name, bool CanReceive, bool CanSend);

/// <summary>Represents a normalized MIDI channel message for future device input and recording.</summary>
public sealed record MidiDeviceMessage(long Timestamp, MidiDeviceMessageKind Kind, int Channel, int Data1, int Data2);

/// <summary>Identifies the supported normalized device-message families.</summary>
public enum MidiDeviceMessageKind
{
    /// <summary>Note-on message.</summary>
    NoteOn,
    /// <summary>Note-off message.</summary>
    NoteOff,
    /// <summary>Pitch-bend message.</summary>
    PitchBend,
    /// <summary>Control-change message.</summary>
    ControlChange,
    /// <summary>Program-change message.</summary>
    ProgramChange,
}

/// <summary>Enumerates and opens replaceable MIDI device implementations.</summary>
public interface IMidiDeviceProvider
{
    /// <summary>Gets currently available endpoints.</summary>
    ValueTask<IReadOnlyList<MidiDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default);
    /// <summary>Opens an input endpoint.</summary>
    ValueTask<IMidiInputDevice> OpenInputAsync(string deviceId, CancellationToken cancellationToken = default);
    /// <summary>Opens an output endpoint.</summary>
    ValueTask<IMidiOutputDevice> OpenOutputAsync(string deviceId, CancellationToken cancellationToken = default);
}

/// <summary>Provides normalized incoming MIDI messages.</summary>
public interface IMidiInputDevice : IAsyncDisposable
{
    /// <summary>Raised for each incoming normalized MIDI message.</summary>
    event EventHandler<MidiDeviceMessage>? MessageReceived;
    /// <summary>Starts receiving messages.</summary>
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops receiving messages.</summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>Provides normalized output and can serve as the playback sink.</summary>
public interface IMidiOutputDevice : IMidiPlaybackOutput, IAsyncDisposable
{
    /// <summary>Sends one normalized device message.</summary>
    ValueTask SendAsync(MidiDeviceMessage message, CancellationToken cancellationToken = default);
}
