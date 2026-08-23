using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoidNote.Application.Shawzin;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Definitions;
using VoidNote.Application.Settings;
using VoidNote.GameBridge.Playback;
using VoidNote.GameBridge.Profiles;
using Microsoft.Extensions.Logging;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Application.Diagnostics;
using VoidNote.Application.Jobs;
using VoidNote.Application.Projects;
using VoidNote.Application.Commands;
using System.Reflection;

namespace VoidNote.App.ViewModels;

/// <summary>Presentation state for the minimal Milestone-D Shawzin Studio.</summary>
public sealed class MainWindowViewModel(IShawzinStudioWorkflow workflow, IMultiShawzinWorkflow multiWorkflow,
    IShawzinEnsembleArranger ensembleArranger, GameBridgePlaybackSession gameBridge,
    KeybindProfileService profileService, ISettingsStore settingsStore, ILogger<MainWindowViewModel> logger,
    AudioLabViewModel audioLab, CreatorModeViewModel creatorMode, MandachordStudioViewModel mandachordStudio,
    IProjectStore projectStore, IProjectRecoveryService recoveryService, IVoidNoteDiagnosticsService diagnostics,
    IBackgroundJobManager jobs, IShawzinValidationTool shawzinValidation,
    IShawzinValidationRecordStore validationRecordStore, IUndoRedoService history,
    ProjectNameEditService projectNameEditor) : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IShawzinStudioWorkflow _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    private ProjectTimeline? _timeline;
    private MidiTrack? _selectedTrack;
    private ShawzinDefinition _selectedInstrument = BuiltInShawzinDefinitions.Default;
    private ShawzinScale _selectedScale = ShawzinScale.Chromatic;
    private StrategyChoice _selectedStrategy = StrategyChoice.All[0];
    private string _status = "Open a MIDI file to begin.";
    private string _compatibility = "Not analyzed";
    private string _songCode = string.Empty;
    private byte[]? _previewWave;
    private ShawzinTrack? _arrangedTrack;
    private IReadOnlyList<ShawzinKeybindProfile> _keybindProfiles = [];
    private ShawzinKeybindProfile? _selectedKeybindProfile;
    private string _gameBridgeStatus = "Not initialized";
    private AppSettings _settings = new();
    private string _profileName = string.Empty;
    private string _string1Key = string.Empty;
    private string _string2Key = string.Empty;
    private string _string3Key = string.Empty;
    private string _fretLeftKey = string.Empty;
    private string _fretMiddleKey = string.Empty;
    private string _fretRightKey = string.Empty;
    private int _shawzinCount = 2;
    private MultiShawzinSplitStrategy _splitStrategy = MultiShawzinSplitStrategy.FullEnsemble;
    private IReadOnlyList<EnsembleTrackViewModel> _ensembleTracks = [];
    private EnsembleTrackViewModel? _selectedEnsembleTrack;
    private string _ensembleReport = "Not split";
    private string _ensembleDetails = string.Empty;
    private ShawzinEnsemble? _ensemble;
    private VoidNoteProject _project = new();
    private string? _projectPath;
    private bool _isDirty;
    private IReadOnlyList<RecentProjectViewModel> _recentProjects = [];
    private RecentProjectViewModel? _selectedRecentProject;
    private RecoveryCandidate? _pendingRecovery;
    private IReadOnlyList<RecoveryCandidate> _pendingRecoveries = [];
    private RecoveryCandidate? _openedRecovery;
    private string _diagnosticsText = "Diagnostics have not been run.";
    private CancellationTokenSource? _autosaveCancellation;
    private Task _autosaveLoop = Task.CompletedTask;
    private readonly SemaphoreSlim _autosaveGate = new(1, 1);
    private string _validationCode = string.Empty;
    private string _validationReport = "Paste a Shawzin code to decode, validate and re-encode it.";
    private string _mappingSequence = string.Empty;
    private string _projectNameDraft = VoidNote.Domain.Projects.ProjectName.Default;

    public event PropertyChangedEventHandler? PropertyChanged;
    public AudioLabViewModel AudioLab { get; } = audioLab;
    public CreatorModeViewModel CreatorMode { get; } = creatorMode;
    public MandachordStudioViewModel MandachordStudio { get; } = mandachordStudio;
    public IReadOnlyList<MidiTrack> Tracks { get; private set; } = [];
    public IReadOnlyList<ShawzinDefinition> Instruments => BuiltInShawzinDefinitions.All;
    public IReadOnlyList<ShawzinScale> Scales { get; } = Enum.GetValues<ShawzinScale>();
    public IReadOnlyList<StrategyChoice> Strategies => StrategyChoice.All;
    public IReadOnlyList<int> ShawzinCounts { get; } = [2, 3, 4];
    public IReadOnlyList<MultiShawzinSplitStrategy> SplitStrategies { get; } = Enum.GetValues<MultiShawzinSplitStrategy>();
    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();
    public IReadOnlyList<AutosaveInterval> AutosaveIntervals { get; } = Enum.GetValues<AutosaveInterval>();
    public IReadOnlyList<string> Cultures { get; } = ["en", "de"];
    public string ApplicationVersion => typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    public VoidNoteProject Project => _project;
    public string ProjectName => _project.Metadata.Title;
    public string ProjectNameDraft
    {
        get => _projectNameDraft;
        set
        {
            if (!Set(ref _projectNameDraft, value)) return;
            OnPropertyChanged(nameof(IsProjectNameEmpty));
            OnPropertyChanged(nameof(IsProjectNameValid));
        }
    }
    public int ProjectNameMaximumLength => VoidNote.Domain.Projects.ProjectName.MaximumLength;
    public bool IsProjectNameEmpty => string.IsNullOrWhiteSpace(ProjectNameDraft);
    public bool IsProjectNameValid => !IsProjectNameEmpty && ProjectNameDraft.Trim().Length <= VoidNote.Domain.Projects.ProjectName.MaximumLength;
    public string ProjectPath => _projectPath ?? "Not saved yet";
    public string ProjectDialogDirectory => ProjectDialogDirectories.GetPreferredDirectory(_settings.Storage);
    public string SuggestedProjectFileName
    {
        get
        {
            var invalid = Path.GetInvalidFileNameChars().ToHashSet();
            var safeName = new string(ProjectName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
            return safeName + ".vns";
        }
    }
    public bool IsDirty { get => _isDirty; private set { if (Set(ref _isDirty, value)) OnPropertyChanged(nameof(ProjectState)); } }
    public string ProjectState => IsDirty ? "Unsaved changes" : "Saved";
    public string ActiveJobs => jobs.Jobs.Count(value => value.State is BackgroundJobState.Queued or BackgroundJobState.Running) is var count && count > 0 ? $"{count} background job(s)" : "No background jobs";
    public IReadOnlyList<RecentProjectViewModel> RecentProjects { get => _recentProjects; private set => Set(ref _recentProjects, value); }
    public RecentProjectViewModel? SelectedRecentProject { get => _selectedRecentProject; set => Set(ref _selectedRecentProject, value); }
    public RecoveryCandidate? PendingRecovery
    {
        get => _pendingRecovery;
        private set
        {
            if (!Set(ref _pendingRecovery, value)) return;
            OnPropertyChanged(nameof(HasPendingRecovery));
            OnPropertyChanged(nameof(PendingRecoveryAutosavedAt));
        }
    }
    public bool HasPendingRecovery => PendingRecovery is not null;
    public DateTimeOffset? PendingRecoveryAutosavedAt => PendingRecovery?.AutosavedAtUtc.ToLocalTime();
    public string DiagnosticsText { get => _diagnosticsText; private set => Set(ref _diagnosticsText, value); }
    public string ValidationCode { get => _validationCode; set => Set(ref _validationCode, value); }
    public string ValidationReport { get => _validationReport; private set => Set(ref _validationReport, value); }
    public string MappingSequence { get => _mappingSequence; private set => Set(ref _mappingSequence, value); }
    public string SelectedCulture { get => _settings.General.Culture; set { _settings = _settings with { General = _settings.General with { Culture = value } }; OnPropertyChanged(); } }
    public ThemePreference SelectedTheme { get => _settings.Appearance.Theme; set { _settings = _settings with { Appearance = _settings.Appearance with { Theme = value } }; OnPropertyChanged(); } }
    public AutosaveInterval SelectedAutosaveInterval { get => _settings.Autosave.Interval; set { _settings = _settings with { Autosave = _settings.Autosave with { Interval = value } }; OnPropertyChanged(); RestartAutosaveLoop(); } }
    public string FfmpegPath { get => _settings.Audio.FfmpegExecutablePath ?? "ffmpeg"; set { _settings = _settings with { Audio = _settings.Audio with { FfmpegExecutablePath = value } }; OnPropertyChanged(); } }
    public string FfplayPath { get => _settings.Audio.FfplayExecutablePath ?? "ffplay"; set { _settings = _settings with { Audio = _settings.Audio with { FfplayExecutablePath = value } }; OnPropertyChanged(); } }
    public string PythonPath { get => _settings.AudioIntelligence.PythonExecutablePath ?? "python"; set { _settings = _settings with { AudioIntelligence = _settings.AudioIntelligence with { PythonExecutablePath = value } }; OnPropertyChanged(); } }
    public string WorkerPath { get => _settings.AudioIntelligence.WorkerScriptPath ?? Path.Combine(AppContext.BaseDirectory, "workers", "python", "voidnote_ai_worker.py"); set { _settings = _settings with { AudioIntelligence = _settings.AudioIntelligence with { WorkerScriptPath = value } }; OnPropertyChanged(); } }

    public MidiTrack? SelectedTrack { get => _selectedTrack; set => Set(ref _selectedTrack, value); }
    public ShawzinDefinition SelectedInstrument { get => _selectedInstrument; set => Set(ref _selectedInstrument, value); }
    public ShawzinScale SelectedScale { get => _selectedScale; set => Set(ref _selectedScale, value); }
    public StrategyChoice SelectedStrategy { get => _selectedStrategy; set => Set(ref _selectedStrategy, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Compatibility { get => _compatibility; private set => Set(ref _compatibility, value); }
    public string SongCode { get => _songCode; private set => Set(ref _songCode, value); }
    public bool HasPreview => _previewWave is { Length: > 44 };
    public IReadOnlyList<ShawzinKeybindProfile> KeybindProfiles { get => _keybindProfiles; private set { _keybindProfiles = value; OnPropertyChanged(); } }
    public ShawzinKeybindProfile? SelectedKeybindProfile
    {
        get => _selectedKeybindProfile;
        set { if (Set(ref _selectedKeybindProfile, value)) LoadProfileEditor(value); }
    }
    public string ProfileName { get => _profileName; set => Set(ref _profileName, value); }
    public string String1Key { get => _string1Key; set => Set(ref _string1Key, value); }
    public string String2Key { get => _string2Key; set => Set(ref _string2Key, value); }
    public string String3Key { get => _string3Key; set => Set(ref _string3Key, value); }
    public string FretLeftKey { get => _fretLeftKey; set => Set(ref _fretLeftKey, value); }
    public string FretMiddleKey { get => _fretMiddleKey; set => Set(ref _fretMiddleKey, value); }
    public string FretRightKey { get => _fretRightKey; set => Set(ref _fretRightKey, value); }
    public string GameBridgeAvailability => $"{(gameBridge.Capability.IsAvailable ? "Yes" : "No")} · {gameBridge.Capability.Backend}: {gameBridge.Capability.Description}";
    public string GameBridgeStatus { get => _gameBridgeStatus; private set => Set(ref _gameBridgeStatus, value); }
    public bool IsGameBridgeArmed => gameBridge.ArmState == VoidNote.GameBridge.Safety.GameBridgeArmState.Armed;
    public bool DisclaimerAcknowledged => _settings.GameBridge.DisclaimerAcknowledged;
    public int ShawzinCount { get => _shawzinCount; set => Set(ref _shawzinCount, value); }
    public MultiShawzinSplitStrategy SplitStrategy { get => _splitStrategy; set => Set(ref _splitStrategy, value); }
    public IReadOnlyList<EnsembleTrackViewModel> EnsembleTracks { get => _ensembleTracks; private set { _ensembleTracks = value; OnPropertyChanged(); } }
    public EnsembleTrackViewModel? SelectedEnsembleTrack
    {
        get => _selectedEnsembleTrack;
        set
        {
            if (Set(ref _selectedEnsembleTrack, value) && value?.Model.ShawzinTrack is not null)
            {
                _arrangedTrack = value.Model.ShawzinTrack;
                SongCode = value.Code;
            }
        }
    }
    public string EnsembleReport { get => _ensembleReport; private set => Set(ref _ensembleReport, value); }
    public string EnsembleDetails { get => _ensembleDetails; private set => Set(ref _ensembleDetails, value); }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        SetProject(AudioLab.Project, null, false);
        _settings = await settingsStore.LoadAsync(token);
        RecentProjects = _settings.RecentProjects.Select(value => new RecentProjectViewModel(value)).ToArray();
        await RefreshPendingRecoveriesAsync(token);
        KeybindProfiles = await profileService.LoadAsync(token);
        SelectedKeybindProfile = KeybindProfiles.FirstOrDefault();
        GameBridgeStatus = "DISARMED";
        AudioLab.ProjectChanged += AudioLabProjectChanged;
        projectNameEditor.ProjectNameChanged += ProjectNameChanged;
        jobs.JobChanged += JobsChanged;
        RestartAutosaveLoop();
        OnPropertyChanged(nameof(GameBridgeAvailability)); OnPropertyChanged(nameof(DisclaimerAcknowledged)); OnPropertyChanged(nameof(SelectedCulture)); OnPropertyChanged(nameof(SelectedTheme)); OnPropertyChanged(nameof(SelectedAutosaveInterval));
    }

    public void NewProject()
    {
        SetProject(new VoidNoteProject { Metadata = new() { Title = "Untitled" } }, null, true);
        Status = "New project created.";
    }

    public void RenameProject()
    {
        if (!IsProjectNameValid) return;
        projectNameEditor.Rename(_project, ProjectNameDraft);
        ProjectNameDraft = ProjectName;
        NotifyHistoryState();
    }

    public void Undo()
    {
        if (history.Undo()) IsDirty = true;
        NotifyHistoryState();
    }

    public void Redo()
    {
        if (history.Redo()) IsDirty = true;
        NotifyHistoryState();
    }

    public bool CanUndo => history.CanUndo;
    public bool CanRedo => history.CanRedo;

    public async Task OpenProjectAsync(string path, CancellationToken token = default)
    {
        var project = await projectStore.LoadAsync(path, token);
        SetProject(project, Path.GetFullPath(path), false);
        RememberDialogDirectory(path);
        await RememberProjectAsync(token);
        Status = $"Opened project '{project.Metadata.Title}'.";
    }

    public async Task SaveProjectAsync(string path, CancellationToken token = default)
    {
        var savedPath = Path.GetFullPath(path);
        var previousPath = _projectPath;
        await _autosaveGate.WaitAsync(token);
        try
        {
            await projectStore.SaveAsync(_project, savedPath, token);
            await recoveryService.CompleteProjectSaveAsync(_project.Id, previousPath, savedPath, token);
            if (_openedRecovery is not null && _openedRecovery.ProjectId == _project.Id)
            {
                await recoveryService.DiscardAsync(_openedRecovery, token);
                _openedRecovery = null;
            }
            _projectPath = savedPath;
            RememberDialogDirectory(savedPath);
            IsDirty = false;
        }
        finally { _autosaveGate.Release(); }
        await RememberProjectAsync(token);
        await RefreshPendingRecoveriesAsync(token);
        OnPropertyChanged(nameof(ProjectPath)); Status = $"Saved project '{ProjectName}'.";
    }

    public Task SaveProjectAsync(CancellationToken token = default) => _projectPath is null
        ? throw new InvalidOperationException("Choose a project file before saving.")
        : SaveProjectAsync(_projectPath, token);

    public async Task OpenSelectedRecentProjectAsync(CancellationToken token = default)
    {
        if (SelectedRecentProject is null || SelectedRecentProject.IsMissing) return;
        await OpenProjectAsync(SelectedRecentProject.Path, token);
    }

    public async Task RecoverAsync(CancellationToken token = default)
    {
        if (PendingRecovery is null) return;
        var candidate = PendingRecovery;
        var project = await recoveryService.RecoverAsync(candidate, token);
        SetProject(project, candidate.OriginalProjectPath, true);
        _openedRecovery = candidate;
        RemovePendingRecovery(candidate);
        Status = $"Recovered '{project.Metadata.Title}'. Save explicitly to keep the recovered version.";
    }

    public async Task DiscardRecoveryAsync(CancellationToken token = default)
    {
        if (PendingRecovery is null) return;
        var candidate = PendingRecovery;
        await recoveryService.DiscardAsync(candidate, token);
        RemovePendingRecovery(candidate);
    }

    public async Task RunDiagnosticsAsync(CancellationToken token = default) => DiagnosticsText = (await diagnostics.RunAsync(token)).ToText();
    public async Task<string> ExportDiagnosticsJsonAsync(CancellationToken token = default) => (await diagnostics.RunAsync(token)).ToJson();
    public async Task SaveSettingsAsync(CancellationToken token = default)
    {
        _settings = _settings with { General = _settings.General with { FirstRunCompleted = true } };
        await settingsStore.SaveAsync(_settings, token); Status = "Settings saved. Language and theme are applied reliably after restart.";
    }

    public void ValidateShawzinCode()
    {
        var report = shawzinValidation.Validate(ValidationCode.Trim(), SelectedInstrument);
        ValidationReport = $"Valid: {report.IsValid}\nInstrument profile: {report.InstrumentProfile}\nEvents: {report.EventCount}\nDuration: {report.DurationSeconds:0.####} s\nSpacing: {report.MinimumSpacingSeconds:0.####}..{report.MaximumSpacingSeconds:0.####} s\nRe-encoded: {report.ReEncodedCode}\n" +
            string.Join(Environment.NewLine, report.Differences.Concat(report.Errors));
    }

    public void GenerateMappingSequence()
    {
        var validation = shawzinValidation.CreateMappingValidation(SelectedInstrument, SelectedScale);
        MappingSequence = validation.Description + Environment.NewLine + Environment.NewLine + "Validation song code:" + Environment.NewLine + validation.SongCode;
        ValidationCode = validation.SongCode;
        SongCode = validation.SongCode;
        Status = $"Generated the real twelve-position {SelectedScale} validation sequence for {SelectedInstrument.DisplayName}.";
    }

    public async Task SaveMappingValidationAsync(bool confirmed, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(MappingSequence)) GenerateMappingSequence();
        await validationRecordStore.SaveAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, SelectedInstrument.Id, SelectedScale, MappingSequence, confirmed,
            "Manual in-game validation; no Warframe process inspection was performed."), token);
        Status = "Local Shawzin mapping validation record saved.";
    }

    public async Task LoadMidiFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var import = await _workflow.ImportMidiFileAsync(path, cancellationToken);
        _timeline = import.Timeline;
        Tracks = import.Tracks;
        _project = new VoidNoteProject { Metadata = new() { Title = Path.GetFileNameWithoutExtension(path) }, Timeline = import.Timeline, MidiTracks = [.. import.Tracks] };
        _projectPath = null; IsDirty = true; AudioLab.LoadProject(_project); OnPropertyChanged(nameof(Project)); OnPropertyChanged(nameof(ProjectName)); OnPropertyChanged(nameof(ProjectPath));
        MandachordStudio.Prepare(import.Timeline, import.Tracks);
        SelectedTrack = Tracks.FirstOrDefault();
        OnPropertyChanged(nameof(Tracks));
        Status = $"Loaded {Tracks.Count} MIDI track(s).";
        Analyze();
    }

    public void Analyze()
    {
        if (SelectedTrack is null || _timeline is null) { Status = "Select a MIDI track first."; return; }
        var analysis = _workflow.Analyze(SelectedTrack, _timeline, SelectedInstrument, SelectedScale);
        var report = analysis.Compatibility;
        Compatibility = $"Playable {report.OverallScore}% · direct {report.DirectlyPlayablePercent}% · octave {report.OctaveFixablePercent}% · substitutions {report.PitchSubstitutionNotes} · expected change {report.ExpectedChangeRatePercent}% · mean pitch error {report.MeanPitchErrorSemitones:0.##} st · conflicts {report.TimingConflicts + report.PolyphonyConflicts + report.ChordConflicts}";
        Status = $"Best scale: {analysis.ScaleCandidates[0].DisplayName}; best transposition: {analysis.TranspositionCandidates[0].Semitones:+#;-#;0}.";
    }

    public void Arrange()
    {
        if (SelectedTrack is null || _timeline is null) { Status = "Select a MIDI track first."; return; }
        var result = _workflow.Arrange(SelectedTrack, _timeline, SelectedInstrument, new ArrangementOptions
        {
            Scale = SelectedScale,
            Strategies = SelectedStrategy.Strategies,
        });
        SongCode = result.Encoding?.Code ?? string.Empty;
        _previewWave = result.Preview?.WaveData;
        _arrangedTrack = result.Arrangement.Track;
        if (_arrangedTrack is not null)
        {
            _project.ShawzinTracks.RemoveAll(value => value.Id == _arrangedTrack.Id);
            _project.ShawzinTracks.Add(_arrangedTrack); IsDirty = true;
        }
        OnPropertyChanged(nameof(HasPreview));
        Status = result.Arrangement.IsSuccess
            ? $"Arranged {result.Arrangement.Report.OutputNoteCount} notes · {result.Arrangement.Report.TotalChangedSourceNotes} changed ({result.Arrangement.Report.ChangeRatePercent}%) · musical similarity {result.Arrangement.Report.MusicalSimilarity.OverallScore}%."
            : $"Arrangement has {result.Arrangement.Report.Changes.Count(value => value.ChangeType == ArrangementChangeType.ConflictUnresolved)} unresolved conflict(s).";
    }

    public void SplitEnsemble()
    {
        if (SelectedTrack is null || _timeline is null) { Status = "Select a MIDI track first."; return; }
        var result = multiWorkflow.Create([SelectedTrack], _timeline, new MultiShawzinSplitOptions
        {
            ShawzinCount = ShawzinCount,
            Strategy = SplitStrategy,
        });
        _ensemble = result.Ensemble;
        foreach (var track in result.Ensemble.Tracks.Select(value => value.ShawzinTrack).OfType<ShawzinTrack>())
            if (_project.ShawzinTracks.All(value => value.Id != track.Id)) _project.ShawzinTracks.Add(track);
        CreatorMode.Prepare(_project, result.Ensemble, result.Export); IsDirty = true;
        _previewWave = result.Preview.WaveData;
        var exports = result.Export.Tracks.ToDictionary(value => value.TrackId);
        EnsembleTracks = result.Ensemble.Tracks.Select(track => new EnsembleTrackViewModel(result.Ensemble, track, ensembleArranger, RefreshEnsemble)
        {
            Code = exports[track.Id].Code ?? string.Empty,
        }).ToArray();
        SelectedEnsembleTrack = EnsembleTracks.FirstOrDefault();
        RefreshEnsemble();
        OnPropertyChanged(nameof(HasPreview));
        Status = $"Created {EnsembleTracks.Count} independent Shawzin tracks.";
    }

    private void RefreshEnsemble()
    {
        if (_ensemble is null) return;
        var export = multiWorkflow.Export(_ensemble);
        var byId = export.Tracks.ToDictionary(value => value.TrackId);
        foreach (var row in EnsembleTracks)
        {
            row.Code = byId[row.Model.Id].Code ?? string.Empty;
            row.NotifyAnalysisChanged();
        }
        _previewWave = multiWorkflow.Preview(_ensemble).WaveData;
        var metrics = _ensemble.OptimizationReport!;
        EnsembleReport = $"Source {metrics.SourceNoteCount} · arranged {metrics.ArrangedNoteCount} · loss {metrics.NoteLossPercent}% · " +
            $"playability {metrics.OverallPlayability}% · musical similarity {metrics.OverallMusicalSimilarity}% · pitch changes {metrics.PitchChangeRatePercent}% · timing changes {metrics.TimingChangeRatePercent}% · continuity {metrics.VoiceContinuityScore}% · balance {metrics.BalanceScore}%";
        EnsembleDetails = string.Join(Environment.NewLine, _ensemble.SplitReport.Assignments.Select(value =>
            $"{value.SourceTime.Ticks}: pitch {value.SourcePitch} → {value.TargetTrackName} ({value.Confidence:P0}) · {value.Reason}")) +
            Environment.NewLine + string.Join(Environment.NewLine, _ensemble.SplitReport.LaterArrangementChanges.Select(value =>
                $"{value.ChangeType}: {value.SourcePitch} → {value.TargetPitch?.ToString() ?? "dropped"} · {value.Reason}"));
        if (SelectedEnsembleTrack is not null) SongCode = SelectedEnsembleTrack.Code;
        OnPropertyChanged(nameof(HasPreview));
    }

    public async Task ArmAsync(bool acknowledgeRisk, CancellationToken token = default)
    {
        if (acknowledgeRisk && !_settings.GameBridge.DisclaimerAcknowledged)
        {
            _settings = _settings with { GameBridge = _settings.GameBridge with { DisclaimerAcknowledged = true } };
            await settingsStore.SaveAsync(_settings, token);
        }
        gameBridge.Arm(_settings.GameBridge.DisclaimerAcknowledged);
        GameBridgeStatus = "ARMED · switch focus to the configured target before Start";
        OnPropertyChanged(nameof(IsGameBridgeArmed)); OnPropertyChanged(nameof(DisclaimerAcknowledged));
    }

    public async Task DisarmAsync() { await gameBridge.StopAsync(); GameBridgeStatus = "DISARMED"; OnPropertyChanged(nameof(IsGameBridgeArmed)); }

    public async Task SaveProfileAsync(CancellationToken token = default)
    {
        var id = SelectedKeybindProfile?.Id ?? Guid.NewGuid();
        var profile = new ShawzinKeybindProfile { Id = id, Name = ProfileName, Bindings = EditorBindings() };
        try { KeybindProfiles = await profileService.AddOrUpdateAsync(KeybindProfiles, profile, token); SelectedKeybindProfile = KeybindProfiles.Single(x => x.Id == id); GameBridgeStatus = $"Saved profile '{profile.Name}'."; }
        catch (Exception exception) { GameBridgeStatus = $"Profile not saved: {exception.Message}"; }
    }

    public async Task DuplicateProfileAsync(CancellationToken token = default)
    {
        if (SelectedKeybindProfile is null) return;
        KeybindProfiles = await profileService.DuplicateAsync(KeybindProfiles, SelectedKeybindProfile.Id, $"{SelectedKeybindProfile.Name} Copy", token);
        SelectedKeybindProfile = KeybindProfiles[^1]; GameBridgeStatus = "Profile duplicated.";
    }

    public async Task DeleteProfileAsync(CancellationToken token = default)
    {
        if (SelectedKeybindProfile is null) return;
        KeybindProfiles = await profileService.DeleteAsync(KeybindProfiles, SelectedKeybindProfile.Id, token);
        SelectedKeybindProfile = KeybindProfiles.FirstOrDefault(); GameBridgeStatus = "Profile deleted.";
    }

    public async Task StartIngameAsync(CancellationToken token = default)
    {
        if (_arrangedTrack is null || SelectedKeybindProfile is null) { GameBridgeStatus = "Arrange a valid track and select a keybind profile first."; return; }
        var value = _settings.GameBridge;
        try
        {
            GameBridgeStatus = "Playing…";
            await gameBridge.PlayAsync(_arrangedTrack, SelectedKeybindProfile,
                new(TimeSpan.FromMilliseconds(value.KeyDownLeadMilliseconds), TimeSpan.FromMilliseconds(value.HoldDurationMilliseconds), TimeSpan.FromMilliseconds(value.ReleaseDelayMilliseconds)),
                value.TargetWindowTitle, value.FocusLossBehavior == TargetFocusLossBehavior.Abort, token);
            GameBridgeStatus = FormatDiagnostics("Completed", gameBridge.LastDiagnostics);
        }
        catch (Exception exception) { logger.LogError(exception, "GameBridge playback failed and was stopped."); GameBridgeStatus = $"Stopped and disarmed: {exception.Message}"; }
        finally { OnPropertyChanged(nameof(IsGameBridgeArmed)); }
    }

    public async Task DryRunAsync(CancellationToken token = default)
    {
        if (_arrangedTrack is null || SelectedKeybindProfile is null) { GameBridgeStatus = "Arrange a valid track and select a keybind profile first."; return; }
        var result = await gameBridge.DryRunAsync(_arrangedTrack, SelectedKeybindProfile, token: token);
        GameBridgeStatus = result.MappingErrors.Count > 0 ? string.Join(" ", result.MappingErrors) : FormatDiagnostics($"Dry run: {result.EventCount} events, {result.InputCount} transitions", result.Diagnostics);
    }

    public async Task TestInputAsync(CancellationToken token = default)
    {
        if (SelectedKeybindProfile is null) { GameBridgeStatus = "Select a keybind profile first."; return; }
        var testTrack = new ShawzinTrack { Name = "Diagnostic input test", ShawzinEvents = [new(Guid.NewGuid(), AbsoluteTime.Zero, new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]))] };
        var result = await gameBridge.DryRunAsync(testTrack, SelectedKeybindProfile, new(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.Zero), token);
        GameBridgeStatus = result.MappingErrors.Count > 0 ? string.Join(" ", result.MappingErrors) : $"Test input recorded {result.InputCount} diagnostic transitions; no real keys were sent.";
    }

    public async Task EmergencyStopAsync() { await gameBridge.EmergencyStopAsync(); GameBridgeStatus = "EMERGENCY STOP · all keys released · DISARMED"; OnPropertyChanged(nameof(IsGameBridgeArmed)); }
    private static string FormatDiagnostics(string prefix, PlaybackDiagnostics? diagnostics) => diagnostics is null ? prefix : $"{prefix} · inputs {diagnostics.InputCount} · aborted {diagnostics.AbortedEvents} · focus losses {diagnostics.FocusLosses} · emergency stops {diagnostics.EmergencyStops}";

    public async Task SavePreviewAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_previewWave is null) { Status = "Arrange a track before saving a preview."; return; }
        await File.WriteAllBytesAsync(path, _previewWave, cancellationToken);
        Status = "Synthetic WAV preview saved.";
    }

    private IReadOnlyDictionary<ShawzinInputBinding, string> EditorBindings() => new Dictionary<ShawzinInputBinding, string>
    {
        [ShawzinInputBinding.String1] = String1Key, [ShawzinInputBinding.String2] = String2Key, [ShawzinInputBinding.String3] = String3Key,
        [ShawzinInputBinding.FretLeft] = FretLeftKey, [ShawzinInputBinding.FretMiddle] = FretMiddleKey, [ShawzinInputBinding.FretRight] = FretRightKey,
    };

    private void LoadProfileEditor(ShawzinKeybindProfile? profile)
    {
        ProfileName = profile?.Name ?? string.Empty;
        String1Key = Get(profile, ShawzinInputBinding.String1); String2Key = Get(profile, ShawzinInputBinding.String2); String3Key = Get(profile, ShawzinInputBinding.String3);
        FretLeftKey = Get(profile, ShawzinInputBinding.FretLeft); FretMiddleKey = Get(profile, ShawzinInputBinding.FretMiddle); FretRightKey = Get(profile, ShawzinInputBinding.FretRight);
    }
    private static string Get(ShawzinKeybindProfile? profile, ShawzinInputBinding binding) => profile is not null && profile.Bindings.TryGetValue(binding, out var key) ? key : string.Empty;

    private void SetProject(VoidNoteProject project, string? path, bool dirty)
    {
        history.Clear();
        _project = project; _projectPath = path; _timeline = project.Timeline; Tracks = project.MidiTracks;
        _projectNameDraft = project.Metadata.Title;
        SelectedTrack = Tracks.FirstOrDefault(); AudioLab.LoadProject(project); MandachordStudio.Prepare(project.Timeline, project.MidiTracks);
        IsDirty = dirty; OnPropertyChanged(nameof(Project)); OnPropertyChanged(nameof(ProjectName)); OnPropertyChanged(nameof(ProjectNameDraft));
        OnPropertyChanged(nameof(IsProjectNameEmpty)); OnPropertyChanged(nameof(IsProjectNameValid)); OnPropertyChanged(nameof(ProjectPath)); OnPropertyChanged(nameof(Tracks));
        NotifyHistoryState();
    }

    private void ProjectNameChanged(object? sender, EventArgs e)
    {
        _projectNameDraft = ProjectName;
        IsDirty = true;
        OnPropertyChanged(nameof(ProjectName)); OnPropertyChanged(nameof(ProjectNameDraft));
        OnPropertyChanged(nameof(SuggestedProjectFileName));
        OnPropertyChanged(nameof(IsProjectNameEmpty)); OnPropertyChanged(nameof(IsProjectNameValid));
    }

    private void RememberDialogDirectory(string projectPath)
    {
        _settings = _settings with { Storage = ProjectDialogDirectories.RememberProjectDirectory(_settings.Storage, projectPath) };
        OnPropertyChanged(nameof(ProjectDialogDirectory));
    }

    private void NotifyHistoryState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private async Task RememberProjectAsync(CancellationToken token)
    {
        if (_projectPath is null) return;
        var recent = VoidNote.Application.Projects.RecentProjects.AddOrUpdate(_settings.RecentProjects, ProjectName, _projectPath, DateTimeOffset.UtcNow);
        _settings = _settings with { RecentProjects = recent, General = _settings.General with { FirstRunCompleted = true } };
        RecentProjects = recent.Select(value => new RecentProjectViewModel(value)).ToArray();
        await settingsStore.SaveAsync(_settings, token);
    }

    private void AudioLabProjectChanged(object? sender, EventArgs e) => IsDirty = true;
    private void JobsChanged(object? sender, BackgroundJob e) => OnPropertyChanged(nameof(ActiveJobs));

    private void RestartAutosaveLoop()
    {
        _autosaveCancellation?.Cancel(); _autosaveCancellation?.Dispose();
        var interval = _settings.Autosave.GetInterval();
        if (interval == Timeout.InfiniteTimeSpan) { _autosaveCancellation = null; _autosaveLoop = Task.CompletedTask; return; }
        _autosaveCancellation = new CancellationTokenSource();
        _autosaveLoop = RunAutosaveLoopAsync(interval, _autosaveCancellation.Token);
    }

    private async Task RunAutosaveLoopAsync(TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                if (IsDirty) await WriteAutosaveAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogError(exception, "Autosave failed"); }
    }

    private async Task WriteAutosaveAsync(CancellationToken token)
    {
        await _autosaveGate.WaitAsync(token);
        try { await recoveryService.WriteAutosaveAsync(_project, _projectPath, token); }
        finally { _autosaveGate.Release(); }
    }

    public async Task ShutdownAsync(CancellationToken token = default)
    {
        _autosaveCancellation?.Cancel();
        try { await _autosaveLoop.WaitAsync(token); } catch (OperationCanceledException) when (_autosaveCancellation?.IsCancellationRequested == true) { }
        await _autosaveGate.WaitAsync(token);
        try { await recoveryService.CompleteCleanShutdownAsync(_project.Id, _projectPath, IsDirty, token); }
        finally { _autosaveGate.Release(); }
        await settingsStore.SaveAsync(_settings, token);
    }

    private async Task RefreshPendingRecoveriesAsync(CancellationToken token)
    {
        var knownPaths = _settings.RecentProjects.Select(value => value.Path)
            .Append(_projectPath)
            .OfType<string>()
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
        _pendingRecoveries = await recoveryService.FindRecoverableAsync(knownPaths, token);
        PendingRecovery = _pendingRecoveries.FirstOrDefault();
    }

    private void RemovePendingRecovery(RecoveryCandidate candidate)
    {
        _pendingRecoveries = _pendingRecoveries.Where(value => !string.Equals(value.AutosavePath, candidate.AutosavePath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).ToArray();
        PendingRecovery = _pendingRecoveries.FirstOrDefault();
    }

    public async ValueTask DisposeAsync()
    {
        AudioLab.ProjectChanged -= AudioLabProjectChanged; projectNameEditor.ProjectNameChanged -= ProjectNameChanged; jobs.JobChanged -= JobsChanged;
        _autosaveCancellation?.Cancel();
        try { await _autosaveLoop; } catch (OperationCanceledException) { }
        _autosaveCancellation?.Dispose(); _autosaveGate.Dispose();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record RecentProjectViewModel(RecentProjectSettings Settings)
{
    public string Name => Settings.Name;
    public string Path => Settings.Path;
    public DateTimeOffset LastOpened => Settings.LastOpenedUtc.ToLocalTime();
    public bool IsMissing => !File.Exists(Settings.Path);
    public string Availability => IsMissing ? "Missing" : "Available";
}

/// <summary>Editable presentation wrapper around one independently arranged ensemble track.</summary>
public sealed class EnsembleTrackViewModel : INotifyPropertyChanged
{
    private readonly ShawzinEnsemble _ensemble;
    private readonly IShawzinEnsembleArranger _arranger;
    private readonly Action _changed;
    private string _code = string.Empty;

    public EnsembleTrackViewModel(ShawzinEnsemble ensemble, ShawzinEnsembleTrack model, IShawzinEnsembleArranger arranger, Action changed)
    { _ensemble = ensemble; Model = model; _arranger = arranger; _changed = changed; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ShawzinEnsembleTrack Model { get; }
    public IReadOnlyList<ShawzinDefinition> Instruments => BuiltInShawzinDefinitions.All;
    public IReadOnlyList<ShawzinScale> Scales { get; } = Enum.GetValues<ShawzinScale>();
    public IReadOnlyList<StrategyChoice> Strategies => StrategyChoice.All;
    public string DisplayName { get => Model.DisplayName; set { Model.DisplayName = value; Notify(); } }
    public ShawzinDefinition Instrument { get => Model.Instrument; set { Model.Instrument = value; Recalculate(); } }
    public ShawzinScale Scale { get => Model.Scale; set { Model.Scale = value; Recalculate(); } }
    public int Transposition { get => Model.TranspositionSemitones; set { Model.TranspositionSemitones = Math.Clamp(value, -12, 12); Recalculate(); } }
    public StrategyChoice Strategy
    {
        get => Strategies.FirstOrDefault(value => value.Strategies == Model.ArrangementStrategies) ?? Strategies[0];
        set { Model.ArrangementStrategies = value.Strategies; Recalculate(); }
    }
    public bool IsMuted { get => Model.IsMuted; set { Model.IsMuted = value; Notify(); _changed(); } }
    public bool IsSolo { get => Model.IsSolo; set { Model.IsSolo = value; Notify(); _changed(); } }
    public int Compatibility => Model.Compatibility?.OverallScore ?? 0;
    public string Code { get => _code; set { _code = value; Notify(); } }
    public string Report => Model.ArrangementReport is null ? "No arrangement" : $"{Model.ArrangementReport.OutputNoteCount}/{Model.ArrangementReport.SourceNoteCount} notes; {Model.ArrangementReport.TotalChangedSourceNotes} changed ({Model.ArrangementReport.ChangeRatePercent}%); similarity {Model.MusicalSimilarity}%";
    public void NotifyAnalysisChanged() { Notify(nameof(Compatibility)); Notify(nameof(Report)); }
    private void Recalculate() { _arranger.RearrangeTrack(_ensemble, Model); Notify(); NotifyAnalysisChanged(); _changed(); }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record StrategyChoice(string Name, ArrangementStrategy Strategies)
{
    public static IReadOnlyList<StrategyChoice> All { get; } =
    [
        new("Strict", ArrangementStrategy.Strict),
        new("Closest Pitch", ArrangementStrategy.ClosestPitch | ArrangementStrategy.PreserveMelody),
        new("Preserve Melody", ArrangementStrategy.OctaveShift | ArrangementStrategy.PreserveMelody),
        new("Drop Lowest", ArrangementStrategy.OctaveShift | ArrangementStrategy.DropLowest),
        new("Drop Highest", ArrangementStrategy.OctaveShift | ArrangementStrategy.DropHighest),
        new("Arpeggiate", ArrangementStrategy.OctaveShift | ArrangementStrategy.Arpeggiate),
        new("Simplify", ArrangementStrategy.OctaveShift | ArrangementStrategy.PreserveMelody | ArrangementStrategy.Simplify),
    ];
}
