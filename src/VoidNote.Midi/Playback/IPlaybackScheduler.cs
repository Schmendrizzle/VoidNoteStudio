using VoidNote.Domain.Music;

namespace VoidNote.Midi.Playback;

/// <summary>Schedules targets relative to a single monotonic-clock anchor.</summary>
public interface IPlaybackScheduler
{
    /// <summary>Captures the current monotonic timestamp.</summary>
    long GetTimestamp();

    /// <summary>Gets precise elapsed time since a captured timestamp.</summary>
    AbsoluteTime GetElapsedTime(long startingTimestamp);

    /// <summary>Waits until an absolute offset from a captured timestamp is due.</summary>
    ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken);
}
