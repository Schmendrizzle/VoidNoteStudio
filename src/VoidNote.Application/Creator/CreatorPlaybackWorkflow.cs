using VoidNote.Domain.Creator;
using VoidNote.Domain.Music;

namespace VoidNote.Application.Creator;

public enum CreatorPlaybackStage { Prepare, CountIn, SyncSignal, Playing, PostRoll, Complete, Stopped }
public sealed record CreatorDryRunReport(CreatorTakePlan Plan, IReadOnlyList<string> ChecklistWarnings, string TimingSummary);
public sealed record CreatorPlaybackProgress(CreatorPlaybackStage Stage, AbsoluteTime Position, string Display);

public interface ICreatorPlaybackClock { Task WaitUntilAsync(AbsoluteTime position, CancellationToken cancellationToken); }
public interface ICreatorTakePlayer
{
    Task PlayAsync(CreatorTake take, AbsoluteTime sourceStart, AbsoluteTime duration, CancellationToken cancellationToken);
    Task StopAsync(bool emergency, CancellationToken cancellationToken);
}

public sealed class SystemCreatorPlaybackClock : ICreatorPlaybackClock
{
    private long _anchor;
    public void Reset() => _anchor = System.Diagnostics.Stopwatch.GetTimestamp();
    public async Task WaitUntilAsync(AbsoluteTime position, CancellationToken cancellationToken)
    {
        if (_anchor == 0) Reset();
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_anchor);
        var remaining = TimeSpan.FromSeconds((double)position.Seconds) - elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Runs the shared creator sequence while delegating media output to existing module adapters.</summary>
public sealed class CreatorPlaybackWorkflow(ICreatorTimingService timing, ICreatorPlaybackClock clock, ICreatorTakePlayer player)
{
    private CancellationTokenSource? _run;
    public event EventHandler<CreatorPlaybackProgress>? ProgressChanged;

    public CreatorDryRunReport DryRun(CreatorSession session, CreatorTake take)
    {
        var plan = timing.Plan(session, take);
        var warnings = take.Checklist.Where(value => value.IsRequired && !value.IsChecked).Select(value => value.Label).ToArray();
        return new(plan, warnings, $"music {plan.Markers.MusicStart.Seconds:0.###}s..{plan.Markers.MusicEnd.Seconds:0.###}s; source offset {plan.Markers.SourceStart.Seconds:0.###}s");
    }

    public async Task RunAsync(CreatorSession session, CreatorTake take, CancellationToken cancellationToken = default)
    {
        var plan = timing.Plan(session, take); _run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); var token = _run.Token;
        take.ChangeStatus(CreatorTakeStatus.Recording, DateTimeOffset.UtcNow, "Creator playback started");
        try
        {
            Report(CreatorPlaybackStage.Prepare, AbsoluteTime.Zero, "PREPARE");
            await clock.WaitUntilAsync(plan.Markers.CountInStart, token); Report(CreatorPlaybackStage.CountIn, plan.Markers.CountInStart, plan.CountInBeats.ToString());
            await clock.WaitUntilAsync(plan.Markers.SyncPoint, token); Report(CreatorPlaybackStage.SyncSignal, plan.Markers.SyncPoint, "SYNC");
            await clock.WaitUntilAsync(plan.Markers.MusicStart, token); Report(CreatorPlaybackStage.Playing, plan.Markers.MusicStart, "PLAYING");
            await player.PlayAsync(take, plan.Markers.SourceStart, new(plan.Markers.MusicEnd.Seconds - plan.Markers.MusicStart.Seconds), token);
            await clock.WaitUntilAsync(plan.Markers.MusicEnd, token); Report(CreatorPlaybackStage.PostRoll, plan.Markers.MusicEnd, "POST-ROLL");
            await clock.WaitUntilAsync(plan.Markers.PostRollEnd, token); take.ChangeStatus(CreatorTakeStatus.Completed, DateTimeOffset.UtcNow, "Creator playback completed");
            Report(CreatorPlaybackStage.Complete, plan.Markers.PostRollEnd, "COMPLETE");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { Report(CreatorPlaybackStage.Stopped, AbsoluteTime.Zero, "STOPPED"); throw; }
        finally { _run.Dispose(); _run = null; }
    }

    public async Task StopAsync(bool emergency = false)
    { _run?.Cancel(); await player.StopAsync(emergency, CancellationToken.None).ConfigureAwait(false); Report(CreatorPlaybackStage.Stopped, AbsoluteTime.Zero, emergency ? "EMERGENCY STOP" : "STOPPED"); }
    private void Report(CreatorPlaybackStage stage, AbsoluteTime position, string display) => ProgressChanged?.Invoke(this, new(stage, position, display));
}
