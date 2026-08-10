using VoidNote.Domain.Creator;
using VoidNote.Domain.Music;

namespace VoidNote.Application.Creator;

public sealed record CreatorTakePlan(
    Guid TakeId,
    string TakeName,
    CreatorSyncMetadata Markers,
    AbsoluteTime Duration,
    int CountInBeats,
    CreatorSourceType SourceType,
    string SourceName,
    string? SongCode,
    bool RequiresGameBridge,
    int ExpectedEventCount);

public interface ICreatorTimingService
{
    CreatorTakePlan Plan(CreatorSession session, CreatorTake take);
    int ToFrame(AbsoluteTime time, int framesPerSecond);
}

/// <summary>Projects every take onto one deterministic session clock.</summary>
public sealed class CreatorTimingService : ICreatorTimingService
{
    public CreatorTakePlan Plan(CreatorSession session, CreatorTake take)
    {
        ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(take);
        var beats = CountInBeats(session);
        var countDuration = session.MasterTimeline.ToAbsoluteTime(session.MasterTimeline.FromBeats(beats)).Seconds;
        var countInStart = session.SyncSettings.PreRoll.Seconds;
        var syncPoint = countInStart + countDuration + session.SyncSettings.ClickCount * session.SyncSettings.ClickInterval.Seconds;
        var musicStart = syncPoint + session.SyncSettings.MusicStartGap.Seconds;
        var (sourceStart, duration) = Range(session, take);
        var markers = new CreatorSyncMetadata
        {
            SessionStart = AbsoluteTime.Zero,
            CountInStart = new(countInStart),
            SyncPoint = new(syncPoint),
            MusicStart = new(musicStart),
            MusicEnd = new(musicStart + duration.Seconds),
            PostRollEnd = new(musicStart + duration.Seconds + session.SyncSettings.PostRoll.Seconds),
            SourceStart = sourceStart,
        };
        take.SyncMetadata = markers;
        return new(take.Id, take.Name, markers, new(markers.PostRollEnd.Seconds), beats, take.SourceType,
            take.SourceName, take.SongCode, take.RequiresGameBridge, take.ExpectedEventCount);
    }

    public int ToFrame(AbsoluteTime time, int framesPerSecond)
    {
        if (framesPerSecond is not (24 or 25 or 30 or 50 or 60)) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        return checked((int)decimal.Round(time.Seconds * framesPerSecond, 0, MidpointRounding.AwayFromZero));
    }

    private static int CountInBeats(CreatorSession session) => session.CountInSettings.Mode switch
    {
        CreatorCountInMode.FourBeats => 4,
        CreatorCountInMode.OneBar => session.MasterTimeline.TimeSignatureChanges[0].Numerator,
        CreatorCountInMode.TwoBars => checked(session.MasterTimeline.TimeSignatureChanges[0].Numerator * 2),
        CreatorCountInMode.CustomBeats => session.CountInSettings.CustomBeats,
        _ => throw new InvalidOperationException("Unknown count-in mode."),
    };

    private static (AbsoluteTime Start, AbsoluteTime Duration) Range(CreatorSession session, CreatorTake take)
    {
        var start = AbsoluteTime.Zero; var end = session.SongDuration;
        if (take.RangeType == CreatorTakeRangeType.Section)
        {
            var section = session.Sections.SingleOrDefault(value => value.Id == take.SectionId)
                ?? throw new InvalidOperationException("The take references an unavailable section.");
            start = section.Start; end = section.End;
        }
        else if (take.RangeType == CreatorTakeRangeType.CustomRange)
        {
            start = take.CustomStart ?? throw new InvalidOperationException("A custom take needs a start.");
            end = take.CustomEnd ?? throw new InvalidOperationException("A custom take needs an end.");
        }
        if (end.Seconds <= start.Seconds) throw new InvalidOperationException("A take range must end after it starts.");
        return (start, new(end.Seconds - start.Seconds));
    }
}
