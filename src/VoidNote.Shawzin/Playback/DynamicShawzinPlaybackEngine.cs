using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Playback;

/// <summary>An extended output port that can select a scale without changing the classic song model.</summary>
public interface IDynamicShawzinPlaybackOutput : IShawzinPlaybackOutput
{
    ValueTask ChangeScaleAsync(ShawzinScaleChangeEvent scaleChange, CancellationToken cancellationToken);
}

/// <summary>Dispatches note and scale events against one absolute scheduler anchor without cumulative drift.</summary>
public sealed class DynamicShawzinPlaybackEngine(IShawzinPlaybackScheduler scheduler, IDynamicShawzinPlaybackOutput output)
{
    private readonly IShawzinPlaybackScheduler _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    private readonly IDynamicShawzinPlaybackOutput _output = output ?? throw new ArgumentNullException(nameof(output));

    public async Task PlayAsync(DynamicShawzinScalePlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.ScaleChangeEvents.Any(value => !value.IsTimingSafe))
            throw new ArgumentException("A dynamic plan containing an unsafe scale change cannot be played.", nameof(plan));
        var timeline = plan.NoteEvents.Select(value => new Scheduled(value.Event.Position, value.Event, null))
            .Concat(plan.ScaleChangeEvents.Select(value => new Scheduled(value.Timestamp, null, value)))
            .OrderBy(value => value.Time.Seconds).ThenBy(value => value.ScaleChange is null ? 1 : 0).ToArray();
        var anchor = _scheduler.GetTimestamp();
        try
        {
            foreach (var scheduled in timeline)
            {
                var lead = scheduled.Note is null ? 0m : (_output as IShawzinPlaybackTimingOutput)?.KeyDownLead.Seconds ?? 0m;
                await _scheduler.WaitUntilAsync(anchor, new AbsoluteTime(Math.Max(0m, scheduled.Time.Seconds - lead)), cancellationToken).ConfigureAwait(false);
                if (scheduled.ScaleChange is not null) await _output.ChangeScaleAsync(scheduled.ScaleChange, cancellationToken).ConfigureAwait(false);
                else if (scheduled.Note!.Chord.Notes.Count == 1) await _output.PlayNoteAsync(scheduled.Note, cancellationToken).ConfigureAwait(false);
                else await _output.PlayChordAsync(scheduled.Note!, cancellationToken).ConfigureAwait(false);
                await _output.PositionChangedAsync(scheduled.Time, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await _output.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private sealed record Scheduled(AbsoluteTime Time, ShawzinEvent? Note, ShawzinScaleChangeEvent? ScaleChange);
}
