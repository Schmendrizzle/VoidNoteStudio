using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Playback;

/// <summary>Identifies virtual Shawzin transport state.</summary>
public enum ShawzinPlaybackState { Stopped, Playing, Paused }

/// <summary>Schedules absolute offsets relative to one monotonic anchor.</summary>
public interface IShawzinPlaybackScheduler
{
    long GetTimestamp();
    AbsoluteTime GetElapsedTime(long startingTimestamp);
    ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken);
}

/// <summary>Receives virtual Shawzin events without assuming an OS or game destination.</summary>
public interface IShawzinPlaybackOutput
{
    ValueTask PlayNoteAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken);
    ValueTask PlayChordAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
    ValueTask PositionChangedAsync(AbsoluteTime position, CancellationToken cancellationToken);
}
