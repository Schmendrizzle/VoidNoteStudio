using System.IO.Compression;
using System.Text.Json.Nodes;
using VoidNote.Application.Commands;
using VoidNote.Application.Creator;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Shawzin;
using VoidNote.Infrastructure.Projects;
using VoidNote.Shawzin.Ensemble;

namespace VoidNote.IntegrationTests;

public sealed class CreatorModeTests
{
    private readonly CreatorTimingService _timing = new();

    [Fact] public void CountInAndSyncTiming_AreDeterministicAndShared()
    {
        var session = Session(); var first = Take("Lead"); var second = Take("Bass"); session.Takes.AddRange([first, second]);
        var a = _timing.Plan(session, first); var b = _timing.Plan(session, second);
        Assert.Equal(4, a.CountInBeats); Assert.Equal(2m, a.Markers.CountInStart.Seconds); Assert.Equal(a.Markers.SyncPoint, b.Markers.SyncPoint);
        Assert.Equal(a.Markers.MusicStart, b.Markers.MusicStart); Assert.Equal(12m + a.Markers.MusicStart.Seconds, a.Markers.MusicEnd.Seconds);
    }

    [Theory]
    [InlineData(CreatorCountInMode.FourBeats, 4)] [InlineData(CreatorCountInMode.OneBar, 4)] [InlineData(CreatorCountInMode.TwoBars, 8)] [InlineData(CreatorCountInMode.CustomBeats, 7)]
    public void CountInModes_ResolveExpectedBeats(CreatorCountInMode mode, int expected)
    { var session = Session(); session.CountInSettings = new() { Mode = mode, CustomBeats = 7 }; Assert.Equal(expected, _timing.Plan(session, Take()).CountInBeats); }

    [Fact] public void PreAndPostRoll_AffectOnlyOuterMarkers()
    { var session = Session(); session.SyncSettings = session.SyncSettings with { PreRoll = new(1.5m), PostRoll = new(4.5m) }; var plan = _timing.Plan(session, Take()); Assert.Equal(1.5m, plan.Markers.CountInStart.Seconds); Assert.Equal(4.5m, plan.Markers.PostRollEnd.Seconds - plan.Markers.MusicEnd.Seconds); }

    [Fact] public void SectionTake_AdjustsSourceAndDurationButNotMusicStart()
    {
        var session = Session(); var section = new CreatorSection { Name = "Chorus", Start = new(3m), End = new(8m) }; session.Sections.Add(section);
        var full = _timing.Plan(session, Take("Full")); var partial = Take("Chorus"); partial.RangeType = CreatorTakeRangeType.Section; partial.SectionId = section.Id; var plan = _timing.Plan(session, partial);
        Assert.Equal(full.Markers.MusicStart, plan.Markers.MusicStart); Assert.Equal(3m, plan.Markers.SourceStart.Seconds); Assert.Equal(5m, plan.Markers.MusicEnd.Seconds - plan.Markers.MusicStart.Seconds);
    }

    [Fact] public void CustomPartialTake_UsesExactRange()
    { var take = Take(); take.RangeType = CreatorTakeRangeType.CustomRange; take.CustomStart = new(1.25m); take.CustomEnd = new(4.75m); var plan = _timing.Plan(Session(), take); Assert.Equal(1.25m, plan.Markers.SourceStart.Seconds); Assert.Equal(3.5m, plan.Markers.MusicEnd.Seconds - plan.Markers.MusicStart.Seconds); }

    [Fact] public void StatusChanges_AreAudited()
    { var take = Take(); var at = DateTimeOffset.Parse("2026-08-10T12:00:00Z"); take.ChangeStatus(CreatorTakeStatus.Ready, at, "validated"); Assert.Equal(CreatorTakeStatus.Ready, take.Status); var change = Assert.Single(take.StatusHistory); Assert.Equal(CreatorTakeStatus.Pending, change.From); Assert.Equal("validated", change.Reason); }

    [Fact] public void Retake_PreservesOriginalAndIncrementsAttempt()
    { var session = Session(); var original = Take("Lead"); original.Status = CreatorTakeStatus.Rejected; session.Takes.Add(original); var retake = new CreatorSessionFactory(_timing).CreateRetake(session, original); Assert.Equal(2, session.Takes.Count); Assert.Equal(2, retake.AttemptNumber); Assert.Equal(original.RetakeGroupId, retake.RetakeGroupId); Assert.Equal(CreatorTakeStatus.Rejected, original.Status); }

    [Fact] public void Checklist_IsConfigurableAndPersistedInModel()
    { var take = Take(); take.Checklist.Add(new() { Label = "OBS recording active", IsRequired = false, IsChecked = true }); Assert.True(take.Checklist[0].IsChecked); Assert.False(take.Checklist[0].IsRequired); }

    [Fact] public void CsvAndJson_ContainDeterministicMarkersAndFrames()
    { var session = Session(); session.Takes.Add(Take("Lead, One")); var service = new CreatorExportService(_timing); var csv = service.ExportCsv(session, 30); var json = service.ExportJson(session, 30); Assert.Contains("\"Lead, One\"", csv); Assert.Contains("MusicStartFrame30", csv); Assert.Contains("\"MusicStartFrame\"", json); Assert.Contains("\"SyncPoint\"", json); }

    [Theory] [InlineData(24, 36)] [InlineData(30, 45)] [InlineData(60, 90)]
    public void FrameCalculation_RoundsHalfAwayFromZero(int fps, int expected) => Assert.Equal(expected, _timing.ToFrame(new(1.5m), fps));

    [Fact] public void SyncWave_IsValidDeterministicPcmAndContainsSignal()
    { var session = Session(); session.Takes.Add(Take()); var exporter = new CreatorExportService(_timing); var a = exporter.ExportSyncWave(session, 8000); var b = exporter.ExportSyncWave(session, 8000); Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(a, 0, 4)); Assert.Equal(a, b); Assert.Contains(a.Skip(44), value => value != 0); }

    [Fact] public void SessionReport_SummarizesAttemptsSectionsCodesAndShawzins()
    { var session = Session(); var a = Take("Lead"); a.Status = CreatorTakeStatus.Completed; a.SongCode = "code-a"; a.Instrument = "Dax"; var b = Take("Bass"); b.Status = CreatorTakeStatus.NeedsRetake; b.Instrument = "Dax"; session.Takes.AddRange([a, b]); session.Sections.Add(new() { Name = "Intro", Start = new(0m), End = new(2m) }); var report = new CreatorExportService(_timing).CreateReport(session); Assert.Equal(2, report.TrackCount); Assert.Equal(1, report.Completed); Assert.Equal(1, report.NeedsRetake); Assert.Equal(1, report.AvailableCodes); Assert.Single(report.Shawzins); }

    [Fact] public void UndoRedo_CoversStatusNotesSectionsAndAssignment()
    {
        var history = new UndoRedoService(); var edits = new CreatorEditService(history); var session = Session(); var take = Take(); session.Takes.Add(take);
        edits.SetNotes(take, "good take"); edits.SetStatus(take, CreatorTakeStatus.Completed, DateTimeOffset.UtcNow); var section = new CreatorSection { Name = "Solo", Start = new(2m), End = new(3m) }; edits.AddSection(session, section); edits.AssignTrack(take, Guid.NewGuid(), "New source");
        Assert.Equal("New source", take.SourceName); history.Undo(); Assert.Equal("Source", take.SourceName); history.Undo(); Assert.Empty(session.Sections); history.Undo(); Assert.Equal(CreatorTakeStatus.Pending, take.Status); history.Undo(); Assert.Empty(take.Notes);
    }

    [Fact] public async Task Persistence_RoundTripsCreatorSession()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "creator.vns"); var project = Project(); var session = Session(project); session.Takes.Add(Take()); project.CreatorSessions.Add(session);
        var store = new VnsProjectStore(); await store.SaveAsync(project, path); var loaded = await store.LoadAsync(path); var restored = Assert.Single(loaded.CreatorSessions);
        Assert.Equal(project.Id, restored.ProjectId); Assert.Single(restored.Takes); Assert.Equal(VoidNoteProject.CurrentFormatVersion, loaded.FormatVersion);
    }

    [Fact] public async Task VersionTwoMigration_AddsCreatorSessionsAndCreatesBackupOnSave()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "v2.vns"); var project = Project(); var store = new VnsProjectStore(); await store.SaveAsync(project, path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update)) { var entry = archive.GetEntry("project.json")!; JsonNode manifest; using (var reader = new StreamReader(entry.Open())) manifest = JsonNode.Parse(await reader.ReadToEndAsync())!; entry.Delete(); manifest["FormatVersion"] = 2; manifest.AsObject().Remove("CreatorSessions"); var replacement = archive.CreateEntry("project.json"); await using var writer = new StreamWriter(replacement.Open()); await writer.WriteAsync(manifest.ToJsonString()); }
        var loaded = await store.LoadAsync(path); Assert.Empty(loaded.CreatorSessions); Assert.Equal(2, loaded.LoadedFormatVersion); await store.SaveAsync(loaded, path); Assert.True(File.Exists(path + ".v2.bak"));
    }

    [Fact] public void DryRun_ReportsSourceCodeEventsTimingAndChecklist()
    { var session = Session(); var take = Take(); take.SongCode = "abc"; take.ExpectedEventCount = 42; take.RequiresGameBridge = true; take.Checklist.Add(new() { Label = "Warframe focused" }); var workflow = new CreatorPlaybackWorkflow(_timing, new ImmediateClock(), new NoopPlayer()); var report = workflow.DryRun(session, take); Assert.Equal(42, report.Plan.ExpectedEventCount); Assert.Equal("abc", report.Plan.SongCode); Assert.True(report.Plan.RequiresGameBridge); Assert.Contains("Warframe focused", report.ChecklistWarnings); }

    [Fact] public void ProjectWizard_PreparesAudioMidiAndShawzinAndAllowsExclusion()
    {
        var project = Project(); var midi = new MidiTrack { Name = "MIDI Bass" }; midi.Events.Add(new(Guid.NewGuid(), MusicalTime.Zero, new(960), 48, 90, MusicalEventSource.ImportedMidi, 1)); project.MidiTracks.Add(midi);
        var shawzin = new ShawzinTrack { Name = "Lead Shawzin" }; shawzin.ShawzinEvents.Add(new(Guid.NewGuid(), new(2m), new ShawzinChord([new(ShawzinString.First, ShawzinFret.None)]))); project.ShawzinTracks.Add(shawzin);
        var audio = new AudioTrack { Name = "Bass Stem", Clips = { new AudioClip { Name = "clip", SourceId = Guid.NewGuid(), Duration = new(5m) } } }; project.AudioTracks.Add(audio);
        var factory = new CreatorSessionFactory(_timing); var sources = factory.GetProjectSources(project); Assert.Equal(3, sources.Count); Assert.Contains(sources, value => value.SourceType == CreatorSourceType.Audio);
        var session = factory.FromProject(project, [new(midi.Id, false), new(shawzin.Id), new(audio.Id)]); Assert.Equal(2, session.Takes.Count); Assert.DoesNotContain(session.Takes, value => value.SourceTrackId == midi.Id);
    }

    [Fact] public async Task PlaybackWorkflow_FollowsStagesAndCompletes()
    { var session = Session(); var take = Take(); var player = new NoopPlayer(); var workflow = new CreatorPlaybackWorkflow(_timing, new ImmediateClock(), player); var stages = new List<CreatorPlaybackStage>(); workflow.ProgressChanged += (_, value) => stages.Add(value.Stage); await workflow.RunAsync(session, take); Assert.Equal([CreatorPlaybackStage.Prepare, CreatorPlaybackStage.CountIn, CreatorPlaybackStage.SyncSignal, CreatorPlaybackStage.Playing, CreatorPlaybackStage.PostRoll, CreatorPlaybackStage.Complete], stages); Assert.Equal(CreatorTakeStatus.Completed, take.Status); Assert.True(player.Played); }

    private static VoidNoteProject Project() => new() { Metadata = new() { Title = "Golden Creator" } };
    private static CreatorSession Session(VoidNoteProject? project = null) { project ??= Project(); return new() { Name = "Session", ProjectId = project.Id, MasterTimeline = project.Timeline, SongDuration = new(12m) }; }
    private static CreatorTake Take(string name = "Take") => new() { Name = name, SourceTrackId = Guid.NewGuid(), SourceName = "Source", SourceType = CreatorSourceType.EnsembleShawzin, Status = CreatorTakeStatus.Pending };
    private sealed class ImmediateClock : ICreatorPlaybackClock { public Task WaitUntilAsync(AbsoluteTime position, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class NoopPlayer : ICreatorTakePlayer { public bool Played { get; private set; } public Task PlayAsync(CreatorTake take, AbsoluteTime sourceStart, AbsoluteTime duration, CancellationToken cancellationToken) { Played = true; return Task.CompletedTask; } public Task StopAsync(bool emergency, CancellationToken cancellationToken) => Task.CompletedTask; }
}
