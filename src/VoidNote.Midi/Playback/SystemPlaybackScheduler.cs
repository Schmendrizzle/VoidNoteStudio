using System.Diagnostics;
using VoidNote.Domain.Music;

namespace VoidNote.Midi.Playback;

/// <summary>Uses the system monotonic clock to schedule absolute playback targets.</summary>
public sealed class SystemPlaybackScheduler : IPlaybackScheduler
{
    /// <inheritdoc />
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <inheritdoc />
    public AbsoluteTime GetElapsedTime(long startingTimestamp)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startingTimestamp;
        return new AbsoluteTime(elapsedTicks / (decimal)Stopwatch.Frequency);
    }

    /// <inheritdoc />
    public async ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = targetOffset.Seconds - GetElapsedTime(startingTimestamp).Seconds;
            if (remaining <= 0m) return;
            await Task.Delay(TimeSpan.FromSeconds((double)remaining), cancellationToken).ConfigureAwait(false);
        }
    }
}
