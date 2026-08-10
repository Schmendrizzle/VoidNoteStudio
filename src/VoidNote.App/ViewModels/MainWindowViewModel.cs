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

namespace VoidNote.App.ViewModels;

/// <summary>Presentation state for the minimal Milestone-D Shawzin Studio.</summary>
public sealed class MainWindowViewModel(IShawzinStudioWorkflow workflow, IMultiShawzinWorkflow multiWorkflow,
    IShawzinEnsembleArranger ensembleArranger, GameBridgePlaybackSession gameBridge,
    KeybindProfileService profileService, ISettingsStore settingsStore, ILogger<MainWindowViewModel> logger,
    AudioLabViewModel audioLab, CreatorModeViewModel creatorMode, MandachordStudioViewModel mandachordStudio) : INotifyPropertyChanged
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
        _settings = await settingsStore.LoadAsync(token);
        KeybindProfiles = await profileService.LoadAsync(token);
        SelectedKeybindProfile = KeybindProfiles.FirstOrDefault();
        GameBridgeStatus = "DISARMED";
        OnPropertyChanged(nameof(GameBridgeAvailability)); OnPropertyChanged(nameof(DisclaimerAcknowledged));
    }

    public async Task LoadMidiFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var import = await _workflow.ImportMidiFileAsync(path, cancellationToken);
        _timeline = import.Timeline;
        Tracks = import.Tracks;
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
        Compatibility = $"{report.OverallScore}/100 · direct {report.DirectlyPlayablePercent}% · octave {report.OctaveFixablePercent}% · unsupported {report.UnsupportedPercent}% · conflicts {report.TimingConflicts + report.PolyphonyConflicts + report.ChordConflicts}";
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
        OnPropertyChanged(nameof(HasPreview));
        Status = result.Arrangement.IsSuccess
            ? $"Arranged {result.Arrangement.Report.OutputNoteCount} notes with {result.Arrangement.Report.Changes.Count} reported changes."
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
        var creatorProject = new VoidNoteProject { Metadata = new ProjectMetadata { Title = "Creator Project" }, Timeline = _timeline };
        CreatorMode.Prepare(creatorProject, result.Ensemble, result.Export);
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
            $"compatibility avg {metrics.AverageCompatibility} / min {metrics.LowestTrackCompatibility} · continuity {metrics.VoiceContinuityScore}% · balance {metrics.BalanceScore}%";
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

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    public string Report => Model.ArrangementReport is null ? "No arrangement" : $"{Model.ArrangementReport.OutputNoteCount}/{Model.ArrangementReport.SourceNoteCount} notes; {Model.ArrangementReport.Changes.Count} changes";
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
