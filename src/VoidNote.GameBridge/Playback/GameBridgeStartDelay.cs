using System.Diagnostics;

namespace VoidNote.GameBridge.Playback;

/// <summary>Describes the safe hand-off from VoidNote to the target window.</summary>
public enum GameBridgeStartPhase
{
    Countdown,
    CheckingFocus,
    Ready,
}

/// <summary>Immutable progress suitable for presentation without affecting the song timeline.</summary>
public sealed record GameBridgeStartProgress(
    GameBridgeStartPhase Phase,
    TimeSpan Total,
    TimeSpan Remaining,
    double Completion)
{
    public int RemainingSeconds => Math.Max(0, (int)Math.Ceiling(Remaining.TotalSeconds));
}

/// <summary>Waits before real playback. Implementations must never generate game input.</summary>
public interface IGameBridgeStartDelay
{
    Task WaitAsync(TimeSpan delay, IProgress<GameBridgeStartProgress>? progress, CancellationToken cancellationToken);
}

/// <summary>Monotonic, cancellation-aware countdown used by production GameBridge sessions.</summary>
public sealed class SystemGameBridgeStartDelay : IGameBridgeStartDelay
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(50);

    public async Task WaitAsync(TimeSpan delay, IProgress<GameBridgeStartProgress>? progress, CancellationToken cancellationToken)
    {
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));

        progress?.Report(new(GameBridgeStartPhase.Countdown, delay, delay, delay == TimeSpan.Zero ? 1d : 0d));
        if (delay == TimeSpan.Zero) return;

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < delay)
        {
            var remaining = delay - stopwatch.Elapsed;
            await Task.Delay(remaining < UpdateInterval ? remaining : UpdateInterval, cancellationToken).ConfigureAwait(false);
            var elapsed = stopwatch.Elapsed > delay ? delay : stopwatch.Elapsed;
            progress?.Report(new(GameBridgeStartPhase.Countdown, delay, delay - elapsed,
                Math.Clamp(elapsed.TotalMilliseconds / delay.TotalMilliseconds, 0d, 1d)));
        }

        progress?.Report(new(GameBridgeStartPhase.Countdown, delay, TimeSpan.Zero, 1d));
    }
}
