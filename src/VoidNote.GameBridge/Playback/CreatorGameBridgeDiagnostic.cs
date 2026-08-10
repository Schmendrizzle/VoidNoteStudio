using VoidNote.Application.Creator;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Profiles;

namespace VoidNote.GameBridge.Playback;

public sealed record CreatorShawzinDryRun(CreatorDryRunReport Creator, DryRunResult GameBridge);

/// <summary>Uses the existing diagnostic GameBridge path for creator Shawzin dry runs.</summary>
public sealed class CreatorGameBridgeDiagnostic(GameBridgePlaybackSession session, ICreatorTimingService timing)
{
    public async Task<CreatorShawzinDryRun> RunAsync(CreatorSession creatorSession, CreatorTake take, ShawzinTrack track,
        ShawzinKeybindProfile profile, CancellationToken cancellationToken = default)
    {
        if (!take.RequiresGameBridge || take.SourceType is not (CreatorSourceType.Shawzin or CreatorSourceType.EnsembleShawzin))
            throw new InvalidOperationException("Only Shawzin creator takes use GameBridge diagnostics.");
        var plan = timing.Plan(creatorSession, take);
        var warnings = take.Checklist.Where(value => value.IsRequired && !value.IsChecked).Select(value => value.Label).ToArray();
        var creator = new CreatorDryRunReport(plan, warnings, $"music {plan.Markers.MusicStart.Seconds:0.###}s..{plan.Markers.MusicEnd.Seconds:0.###}s");
        var diagnostic = await session.DryRunAsync(track, profile, token: cancellationToken).ConfigureAwait(false);
        return new(creator, diagnostic);
    }
}

/// <summary>Creator source-player adapter that retains the existing armed GameBridge and emergency-stop path.</summary>
public sealed class CreatorGameBridgeTakePlayer(
    GameBridgePlaybackSession session,
    IReadOnlyDictionary<Guid, ShawzinTrack> tracks,
    ShawzinKeybindProfile profile,
    GameBridgeTimingOptions timing,
    string targetTitle,
    bool requireFocus) : ICreatorTakePlayer
{
    public Task PlayAsync(CreatorTake take, VoidNote.Domain.Music.AbsoluteTime sourceStart,
        VoidNote.Domain.Music.AbsoluteTime duration, CancellationToken cancellationToken)
    {
        if (!tracks.TryGetValue(take.SourceTrackId, out var track)) throw new InvalidOperationException("The Creator take's Shawzin source is unavailable.");
        return session.PlayRangeAsync(track, profile, timing, targetTitle, requireFocus, sourceStart, duration, cancellationToken);
    }

    public Task StopAsync(bool emergency, CancellationToken cancellationToken) => emergency ? session.EmergencyStopAsync() : session.StopAsync();
}
