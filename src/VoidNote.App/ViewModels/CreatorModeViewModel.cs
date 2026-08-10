using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoidNote.Application.Creator;
using VoidNote.Domain.Creator;
using VoidNote.Domain.Projects;
using VoidNote.Shawzin.Ensemble;
using VoidNote.GameBridge.Playback;
using VoidNote.GameBridge.Profiles;
using VoidNote.Application.Settings;
using VoidNote.Domain.Shawzin;

namespace VoidNote.App.ViewModels;

public sealed class CreatorModeViewModel(ICreatorSessionFactory factory, ICreatorTimingService timing, ICreatorExportService exports,
    GameBridgePlaybackSession gameBridge, KeybindProfileService profiles, ISettingsStore settingsStore) : INotifyPropertyChanged
{
    private VoidNoteProject? _project; private ShawzinEnsemble? _ensemble; private EnsembleExportReport? _ensembleExport;
    private CreatorSession? _session; private CreatorTakeRowViewModel? _selectedTake; private string _status = "Create a Multi-Shawzin ensemble first.";
    private IReadOnlyDictionary<Guid, ShawzinTrack> _shawzinTracks = new Dictionary<Guid, ShawzinTrack>(); private CreatorPlaybackWorkflow? _playback;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<CreatorTrackChoiceViewModel> TrackChoices { get; private set; } = [];
    public IReadOnlyList<CreatorTakeRowViewModel> Takes { get; private set; } = [];
    public CreatorTakeRowViewModel? SelectedTake { get => _selectedTake; set { _selectedTake = value; Notify(); Notify(nameof(Preparation)); } }
    public string SessionName => _session?.Name ?? "No creator session";
    public string Status { get => _status; private set { _status = value; Notify(); } }
    public string Progress => _session is null ? "0 / 0" : $"{_session.Takes.Count(value => value.Status == CreatorTakeStatus.Completed)} / {_session.Takes.Count} completed";
    public string Preparation
    {
        get
        {
            if (_session is null || SelectedTake is null) return "Select a take.";
            var take = SelectedTake.Model; var plan = timing.Plan(_session, take);
            return $"Source: {take.SourceName}\nShawzin: {take.Instrument}\nScale: {take.Scale}\nSection: {SelectedTake.Section}\n" +
                $"Duration: {plan.Markers.MusicEnd.Seconds - plan.Markers.MusicStart.Seconds:0.###} s\nCount-in: {plan.CountInBeats} beats\n" +
                $"Pre-roll: {_session.SyncSettings.PreRoll.Seconds:0.###} s\nPost-roll: {_session.SyncSettings.PostRoll.Seconds:0.###} s\n" +
                $"GameBridge: {(take.RequiresGameBridge ? "required / diagnostic dry run available" : "not required")}\nCode: {(string.IsNullOrEmpty(take.SongCode) ? "none" : take.SongCode)}";
        }
    }

    public void Prepare(VoidNoteProject project, ShawzinEnsemble ensemble, EnsembleExportReport export)
    {
        _project = project; _ensemble = ensemble; _ensembleExport = export;
        _shawzinTracks = ensemble.Tracks.Where(value => value.ShawzinTrack is not null).ToDictionary(value => value.Id, value => value.ShawzinTrack!);
        TrackChoices = ensemble.Tracks.Select(value => new CreatorTrackChoiceViewModel(value.Id, value.DisplayName)).ToArray();
        Notify(nameof(TrackChoices)); Status = "Choose tracks, then create the Creator Session.";
    }

    public void CreateSession()
    {
        if (_project is null || _ensemble is null || _ensembleExport is null) { Status = "Create a Multi-Shawzin ensemble first."; return; }
        _session = factory.FromEnsemble(_project, _ensemble, _ensembleExport,
            TrackChoices.Select(value => new CreatorTrackSelection(value.TrackId, value.IsIncluded)).ToArray(), $"{_ensemble.Name} Creator Session");
        Refresh(); Status = "Creator Session ready. All takes share the same Music Start.";
    }

    public void MarkComplete() { ChangeStatus(CreatorTakeStatus.Completed, "Manually completed"); }
    public void MarkNeedsRetake() { ChangeStatus(CreatorTakeStatus.NeedsRetake, "Retake requested"); }
    public async Task StartTakeAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null || SelectedTake is null) return;
        if (!SelectedTake.Model.RequiresGameBridge) { Status = "Audio/MIDI take playback uses its existing preview workspace; the deterministic Creator plan is ready."; return; }
        try
        {
            var profile = (await profiles.LoadAsync(cancellationToken)).FirstOrDefault() ?? throw new InvalidOperationException("No keybind profile is available.");
            var settings = await settingsStore.LoadAsync(cancellationToken); var game = settings.GameBridge;
            var player = new CreatorGameBridgeTakePlayer(gameBridge, _shawzinTracks, profile,
                new(TimeSpan.FromMilliseconds(game.KeyDownLeadMilliseconds), TimeSpan.FromMilliseconds(game.HoldDurationMilliseconds), TimeSpan.FromMilliseconds(game.ReleaseDelayMilliseconds)),
                game.TargetWindowTitle, game.FocusLossBehavior == TargetFocusLossBehavior.Abort);
            var clock = new SystemCreatorPlaybackClock(); clock.Reset(); _playback = new CreatorPlaybackWorkflow(timing, clock, player);
            _playback.ProgressChanged += (_, value) => Status = value.Display;
            await _playback.RunAsync(_session, SelectedTake.Model, cancellationToken); Refresh();
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException)
        { Status = exception.Message; }
    }
    public async Task EmergencyStopAsync() { if (_playback is not null) await _playback.StopAsync(true); else await gameBridge.EmergencyStopAsync(); Status = "EMERGENCY STOP · all keys released · DISARMED"; }
    public void CreateRetake()
    {
        if (_session is null || SelectedTake is null) return;
        factory.CreateRetake(_session, SelectedTake.Model); Refresh(); Status = "New attempt added; earlier attempts were preserved.";
    }
    public CreatorDryRunReport? DryRun()
    {
        if (_session is null || SelectedTake is null) return null;
        var plan = timing.Plan(_session, SelectedTake.Model); var warnings = SelectedTake.Model.Checklist.Where(value => value.IsRequired && !value.IsChecked).Select(value => value.Label).ToArray();
        Status = $"Dry run: {plan.ExpectedEventCount} events, Music Start {plan.Markers.MusicStart.Seconds:0.###} s, {(plan.RequiresGameBridge ? "GameBridge diagnostic required" : "preview playback")}.";
        return new(plan, warnings, $"{plan.Markers.MusicStart.Seconds:0.###}..{plan.Markers.MusicEnd.Seconds:0.###} s");
    }
    public string ExportJson(int fps = 30) => _session is null ? "[]" : exports.ExportJson(_session, fps);
    public string ExportCsv(int fps = 30) => _session is null ? string.Empty : exports.ExportCsv(_session, fps);
    public byte[] ExportWave() => _session is null ? [] : exports.ExportSyncWave(_session);
    public string SelectedSongCode => SelectedTake?.Model.SongCode ?? string.Empty;

    private void ChangeStatus(CreatorTakeStatus status, string reason)
    {
        if (SelectedTake is null) return; SelectedTake.Model.ChangeStatus(status, DateTimeOffset.UtcNow, reason); Refresh(); Status = reason;
    }
    private void Refresh()
    {
        Takes = _session?.Takes.Select(value => new CreatorTakeRowViewModel(value, _session, timing)).ToArray() ?? [];
        SelectedTake = Takes.LastOrDefault(); Notify(nameof(Takes)); Notify(nameof(SessionName)); Notify(nameof(Progress)); Notify(nameof(Preparation));
    }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class CreatorTrackChoiceViewModel(Guid trackId, string name) { public Guid TrackId { get; } = trackId; public string Name { get; } = name; public bool IsIncluded { get; set; } = true; }
public sealed class CreatorTakeRowViewModel
{
    private readonly CreatorSession _session; private readonly ICreatorTimingService _timing;
    public CreatorTakeRowViewModel(CreatorTake model, CreatorSession session, ICreatorTimingService timing) { Model = model; _session = session; _timing = timing; }
    public CreatorTake Model { get; } public string Name => $"{Model.Name} · Take {Model.AttemptNumber}"; public CreatorTakeStatus Status => Model.Status;
    public string Instrument => Model.Instrument; public string Section => Model.SectionId is null ? "Full song" : _session.Sections.FirstOrDefault(value => value.Id == Model.SectionId)?.Name ?? "Custom";
    public decimal Duration => _timing.Plan(_session, Model).Markers.MusicEnd.Seconds - _timing.Plan(_session, Model).Markers.MusicStart.Seconds;
    public bool CodeAvailable => !string.IsNullOrWhiteSpace(Model.SongCode); public string Notes { get => Model.Notes; set => Model.Notes = value; }
}
