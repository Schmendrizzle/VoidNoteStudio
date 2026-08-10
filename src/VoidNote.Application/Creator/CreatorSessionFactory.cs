using VoidNote.Domain.Creator;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Shawzin.Ensemble;

namespace VoidNote.Application.Creator;

public sealed record CreatorTrackSelection(Guid TrackId, bool IsIncluded = true);
public sealed record CreatorSourceCandidate(Guid TrackId, string Name, CreatorSourceType SourceType, AbsoluteTime Duration, int EventCount, string Provenance);

public interface ICreatorSessionFactory
{
    CreatorSession FromEnsemble(VoidNoteProject project, ShawzinEnsemble ensemble, EnsembleExportReport export,
        IReadOnlyCollection<CreatorTrackSelection>? selection = null, string? name = null, DateTimeOffset? now = null);
    IReadOnlyList<CreatorSourceCandidate> GetProjectSources(VoidNoteProject project);
    CreatorSession FromProject(VoidNoteProject project, IReadOnlyCollection<CreatorTrackSelection>? selection = null,
        string name = "Creator Session", DateTimeOffset? now = null);
    CreatorTake CreateRetake(CreatorSession session, CreatorTake original, DateTimeOffset? now = null);
}

public sealed class CreatorSessionFactory(ICreatorTimingService timing) : ICreatorSessionFactory
{
    public IReadOnlyList<CreatorSourceCandidate> GetProjectSources(VoidNoteProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var midi = project.MidiTracks.Select(track => new CreatorSourceCandidate(track.Id, track.Name, CreatorSourceType.Midi,
            EventDuration(project.Timeline, track.Events), track.Events.Count, Provenance(track)));
        var shawzin = project.ShawzinTracks.Select(track => new CreatorSourceCandidate(track.Id, track.Name, CreatorSourceType.Shawzin,
            new(track.ShawzinEvents.Select(value => value.Position.Seconds).DefaultIfEmpty(0m).Max()), track.ShawzinEvents.Count, "Shawzin"));
        var audio = project.AudioTracks.Select(track => new CreatorSourceCandidate(track.Id, track.Name, CreatorSourceType.Audio,
            new(track.Clips.Select(value => project.Timeline.ToAbsoluteTime(value.Start).Seconds + value.Duration.Seconds).DefaultIfEmpty(0m).Max()), 0,
            project.StemSets.SelectMany(value => value.StemTracks).Any(stem => track.Clips.Any(clip => clip.SourceId == stem.AudioSourceId)) ? "Separated stem -> Audio track" : "Audio track"));
        return midi.Concat(shawzin).Concat(audio).OrderBy(value => value.Name).ThenBy(value => value.TrackId).ToArray();
    }

    public CreatorSession FromProject(VoidNoteProject project, IReadOnlyCollection<CreatorTrackSelection>? selection = null,
        string name = "Creator Session", DateTimeOffset? now = null)
    {
        var included = selection?.Where(value => value.IsIncluded).Select(value => value.TrackId).ToHashSet(); var instant = now ?? DateTimeOffset.UtcNow;
        var sources = GetProjectSources(project).Where(value => included is null || included.Contains(value.TrackId)).ToArray();
        var session = new CreatorSession { Name = name, ProjectId = project.Id, MasterTimeline = project.Timeline, CreatedAt = instant,
            ModifiedAt = instant, SongDuration = new(sources.Select(value => value.Duration.Seconds).DefaultIfEmpty(0m).Max()) };
        foreach (var source in sources) session.Takes.Add(new CreatorTake { Name = source.Name, SourceTrackId = source.TrackId,
            SourceType = source.SourceType, SourceName = source.Name, SourceProvenance = source.Provenance, CreatedAt = instant,
            Status = CreatorTakeStatus.Ready, ExpectedEventCount = source.EventCount,
            RequiresGameBridge = source.SourceType == CreatorSourceType.Shawzin });
        foreach (var take in session.Takes) timing.Plan(session, take); project.CreatorSessions.Add(session); return session;
    }
    public CreatorSession FromEnsemble(VoidNoteProject project, ShawzinEnsemble ensemble, EnsembleExportReport export,
        IReadOnlyCollection<CreatorTrackSelection>? selection = null, string? name = null, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(ensemble); ArgumentNullException.ThrowIfNull(export);
        var included = selection?.Where(value => value.IsIncluded).Select(value => value.TrackId).ToHashSet();
        var instant = now ?? DateTimeOffset.UtcNow;
        var physicalDuration = ensemble.Tracks.SelectMany(value => value.ShawzinTrack?.ShawzinEvents ?? [])
            .Select(value => value.Position.Seconds + 0.0625m).DefaultIfEmpty(0m).Max();
        var normalizedDuration = ensemble.Tracks.Select(value => EventDuration(project.Timeline, value.SourceTrack.Events).Seconds).DefaultIfEmpty(0m).Max();
        var songDuration = Math.Max(physicalDuration, normalizedDuration);
        var session = new CreatorSession
        {
            Name = name ?? ensemble.Name,
            ProjectId = project.Id,
            MasterTimeline = project.Timeline,
            CreatedAt = instant,
            ModifiedAt = instant,
            SongDuration = new AbsoluteTime(songDuration),
        };
        var exported = export.Tracks.ToDictionary(value => value.TrackId);
        foreach (var track in ensemble.Tracks.Where(value => included is null || included.Contains(value.Id)))
        {
            var code = exported.GetValueOrDefault(track.Id);
            session.Takes.Add(new CreatorTake
            {
                Name = track.DisplayName,
                SourceTrackId = track.Id,
                SourceType = CreatorSourceType.EnsembleShawzin,
                SourceName = track.SourceTrack.Name,
                SourceProvenance = Provenance(track.SourceTrack),
                Instrument = track.Instrument.DisplayName,
                ShawzinDefinitionId = track.Instrument.Id,
                Scale = track.Scale.ToString(),
                Transposition = track.TranspositionSemitones,
                ArrangementStrategy = track.ArrangementStrategies.ToString(),
                SongCode = code?.Code,
                Status = code?.IsValid == true ? CreatorTakeStatus.Ready : CreatorTakeStatus.Pending,
                CreatedAt = instant,
                ExpectedEventCount = code?.EventCount ?? 0,
                RequiresGameBridge = true,
                Checklist =
                [
                    new() { Label = "Correct Shawzin selected" }, new() { Label = "Correct keybind profile" },
                    new() { Label = "Warframe focused" }, new() { Label = "OBS recording active", IsRequired = false },
                    new() { Label = "Correct camera", IsRequired = false }, new() { Label = "Correct section" },
                ],
            });
        }
        foreach (var take in session.Takes) timing.Plan(session, take);
        project.CreatorSessions.Add(session);
        return session;
    }

    public CreatorTake CreateRetake(CreatorSession session, CreatorTake original, DateTimeOffset? now = null)
    {
        if (!session.Takes.Contains(original)) throw new InvalidOperationException("The original take does not belong to this session.");
        var attempt = session.Takes.Where(value => value.RetakeGroupId == original.RetakeGroupId).Max(value => value.AttemptNumber) + 1;
        var retake = new CreatorTake
        {
            Name = original.Name, SourceTrackId = original.SourceTrackId, SourceType = original.SourceType,
            SourceName = original.SourceName, SourceProvenance = original.SourceProvenance, Instrument = original.Instrument,
            ShawzinDefinitionId = original.ShawzinDefinitionId, Scale = original.Scale, Transposition = original.Transposition,
            ArrangementStrategy = original.ArrangementStrategy, SongCode = original.SongCode, SectionId = original.SectionId,
            RangeType = original.RangeType, CustomStart = original.CustomStart, CustomEnd = original.CustomEnd,
            AttemptNumber = attempt, RetakeGroupId = original.RetakeGroupId, CreatedAt = now ?? DateTimeOffset.UtcNow,
            TimingOffset = original.TimingOffset, ExpectedEventCount = original.ExpectedEventCount,
            RequiresGameBridge = original.RequiresGameBridge, Status = CreatorTakeStatus.Pending,
            Checklist = original.Checklist.Select(value => new CreatorChecklistItem { Label = value.Label, IsRequired = value.IsRequired }).ToList(),
        };
        timing.Plan(session, retake); session.Takes.Add(retake); session.ModifiedAt = now ?? DateTimeOffset.UtcNow;
        return retake;
    }

    private static string Provenance(MidiTrack track)
    {
        var origins = track.Events.Select(value => value.Source.ToString()).Distinct().Order().ToArray();
        var audio = track.Events.Select(value => value.AudioProvenance).FirstOrDefault(value => value is not null);
        return audio is null ? string.Join(" -> ", origins) : $"Audio {audio.SourceAudioId} -> {string.Join(" -> ", origins)}";
    }

    private static AbsoluteTime EventDuration(ProjectTimeline timeline, IReadOnlyList<MusicalEvent> events) => new(events
        .Select(value => timeline.ToAbsoluteTime(value.StartTime + value.Duration).Seconds).DefaultIfEmpty(0m).Max());
}
