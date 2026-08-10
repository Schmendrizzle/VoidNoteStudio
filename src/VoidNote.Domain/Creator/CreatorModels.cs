using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Domain.Creator;

public enum CreatorSourceType { Shawzin, EnsembleShawzin, Audio, Midi, FutureMandachord, Mandachord }
public enum CreatorTakeStatus { Pending, Ready, Recording, Completed, NeedsRetake, Rejected }
public enum CreatorCountInMode { FourBeats, OneBar, TwoBars, CustomBeats }
public enum CreatorTakeRangeType { FullSong, Section, CustomRange }

public sealed record CreatorCountInSettings
{
    public CreatorCountInMode Mode { get; init; } = CreatorCountInMode.FourBeats;
    public int CustomBeats { get; init; } = 4;
    public bool VisualEnabled { get; init; } = true;
    public bool AudioEnabled { get; init; } = true;
}

public sealed record CreatorSyncSettings
{
    public AbsoluteTime PreRoll { get; init; } = new(2m);
    public AbsoluteTime PostRoll { get; init; } = new(3m);
    public int ClickCount { get; init; } = 3;
    public AbsoluteTime ClickInterval { get; init; } = new(0.4m);
    public AbsoluteTime MusicStartGap { get; init; } = new(0.4m);
    public bool IncludeMusicStartMarkerInWave { get; init; } = true;
}

public sealed record CreatorSyncMetadata
{
    public AbsoluteTime SessionStart { get; init; }
    public AbsoluteTime CountInStart { get; init; }
    public AbsoluteTime SyncPoint { get; init; }
    public AbsoluteTime MusicStart { get; init; }
    public AbsoluteTime MusicEnd { get; init; }
    public AbsoluteTime PostRollEnd { get; init; }
    public AbsoluteTime SourceStart { get; init; }
}

public sealed record CreatorStatusChange
{
    public CreatorTakeStatus From { get; init; }
    public CreatorTakeStatus To { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class CreatorChecklistItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public bool IsChecked { get; set; }
}

public sealed class CreatorSection : ProjectItem
{
    public AbsoluteTime Start { get; set; }
    public AbsoluteTime End { get; set; }

    public void Validate()
    {
        if (End.Seconds <= Start.Seconds) throw new InvalidOperationException("A creator section must end after it starts.");
    }
}

public sealed class CreatorTake : ProjectItem
{
    public Guid SourceTrackId { get; set; }
    public CreatorSourceType SourceType { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceProvenance { get; set; } = string.Empty;
    public string Instrument { get; set; } = string.Empty;
    public string ShawzinDefinitionId { get; set; } = string.Empty;
    public string Scale { get; set; } = string.Empty;
    public int Transposition { get; set; }
    public string ArrangementStrategy { get; set; } = string.Empty;
    public string? SongCode { get; set; }
    public Guid? SectionId { get; set; }
    public CreatorTakeRangeType RangeType { get; set; }
    public AbsoluteTime? CustomStart { get; set; }
    public AbsoluteTime? CustomEnd { get; set; }
    public CreatorTakeStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int AttemptNumber { get; set; } = 1;
    public Guid RetakeGroupId { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public AbsoluteTime TimingOffset { get; set; }
    public CreatorSyncMetadata? SyncMetadata { get; set; }
    public int ExpectedEventCount { get; set; }
    public bool RequiresGameBridge { get; set; }
    public List<CreatorStatusChange> StatusHistory { get; init; } = [];
    public List<CreatorChecklistItem> Checklist { get; init; } = [];
    public Guid? MandachordArrangementId { get; set; }
    public string MandachordPreset { get; set; } = string.Empty;
    public Guid? MandachordSoundSetId { get; set; }
    public string MandachordSection { get; set; } = string.Empty;

    public void ChangeStatus(CreatorTakeStatus status, DateTimeOffset changedAt, string reason = "")
    {
        if (status == Status) return;
        StatusHistory.Add(new CreatorStatusChange { From = Status, To = status, ChangedAt = changedAt, Reason = reason });
        Status = status;
    }
}

/// <summary>A deterministic multi-take recording plan belonging to one VoidNote project.</summary>
public sealed class CreatorSession : ProjectItem
{
    public Guid ProjectId { get; init; }
    public ProjectTimeline MasterTimeline { get; init; } = ProjectTimeline.CreateDefault();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CreatorTake> Takes { get; init; } = [];
    public List<CreatorSection> Sections { get; init; } = [];
    public CreatorSyncSettings SyncSettings { get; set; } = new();
    public CreatorCountInSettings CountInSettings { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public AbsoluteTime SongDuration { get; set; }

    public void Validate()
    {
        if (ProjectId == Guid.Empty) throw new InvalidOperationException("A creator session must reference a project.");
        if (SyncSettings.ClickCount < 1) throw new InvalidOperationException("A sync signal needs at least one click.");
        if (CountInSettings.Mode == CreatorCountInMode.CustomBeats && CountInSettings.CustomBeats < 1)
            throw new InvalidOperationException("A custom count-in needs at least one beat.");
        foreach (var section in Sections) section.Validate();
        if (Takes.Any(take => take.SourceTrackId == Guid.Empty || take.AttemptNumber < 1))
            throw new InvalidOperationException("Creator takes require a source and a positive attempt number.");
        if (Takes.GroupBy(take => new { take.RetakeGroupId, take.AttemptNumber }).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Retake attempt numbers must be unique inside their history group.");
    }
}
