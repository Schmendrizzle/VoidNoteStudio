using System.Diagnostics;
using VoidNote.Domain.Music;

namespace VoidNote.Shawzin.Playback;

/// <summary>Uses <see cref="Stopwatch"/> as a monotonic scheduling clock.</summary>
public sealed class SystemShawzinPlaybackScheduler : IShawzinPlaybackScheduler
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();
    public AbsoluteTime GetElapsedTime(long startingTimestamp) =>
        new((decimal)Stopwatch.GetElapsedTime(startingTimestamp).Ticks / TimeSpan.TicksPerSecond);

    public async ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = targetOffset.Seconds - GetElapsedTime(startingTimestamp).Seconds;
            if (remaining <= 0m) return;
            var delay = TimeSpan.FromSeconds((double)Math.Min(remaining, 0.05m));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
