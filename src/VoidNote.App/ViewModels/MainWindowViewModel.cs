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

namespace VoidNote.App.ViewModels;

/// <summary>Presentation state for the minimal Milestone-D Shawzin Studio.</summary>
public sealed class MainWindowViewModel(IShawzinStudioWorkflow workflow, GameBridgePlaybackSession gameBridge,
    KeybindProfileService profileService, ISettingsStore settingsStore, ILogger<MainWindowViewModel> logger) : INotifyPropertyChanged
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

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<MidiTrack> Tracks { get; private set; } = [];
    public IReadOnlyList<ShawzinDefinition> Instruments => BuiltInShawzinDefinitions.All;
    public IReadOnlyList<ShawzinScale> Scales { get; } = Enum.GetValues<ShawzinScale>();
    public IReadOnlyList<StrategyChoice> Strategies => StrategyChoice.All;

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
