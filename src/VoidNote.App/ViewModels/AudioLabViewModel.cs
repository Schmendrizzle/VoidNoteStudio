using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using VoidNote.Application.Audio;
using VoidNote.Application.Commands;
using VoidNote.Application.Jobs;
using VoidNote.Audio.Import;
using VoidNote.Audio.Playback;
using VoidNote.Audio.Waveforms;
using VoidNote.Audio.Intelligence;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.App.ViewModels;

/// <summary>UI-independent presentation state for the minimal Audio Lab workspace.</summary>
public sealed class AudioLabViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IAudioImportService _import; private readonly IWaveformGenerator _waveforms; private readonly IWaveformCache _cache;
    private readonly IBackgroundJobManager _jobs; private readonly IAudioDeviceProvider _devices; private readonly AudioPlaybackEngine _playback;
    private readonly IUndoRedoService _history; private readonly ILogger<AudioLabViewModel> _logger; private CancellationTokenSource? _operation;
    private readonly IAudioIntelligenceWorkflow _intelligence; private readonly IAudioSeparationEngine _separationEngine; private readonly IAudioTranscriptionEngine _transcriptionEngine;
    private readonly AudioStemMixPreview _stemMix;
    private IReadOnlyList<AudioTrackRowViewModel> _tracks = []; private AudioTrackRowViewModel? _selected; private WaveformData? _waveform;
    private IReadOnlyList<StemRowViewModel> _stems = []; private StemRowViewModel? _selectedStem;
    private string _status = "Import WAV, FLAC or MP3 audio to begin."; private double _progress; private double _zoom = 1;
    private double _playhead; private double _selectionStart; private double _selectionEnd;
    private string _engineStatus = "AI engines not checked."; private string _transcriptionMetrics = "No transcription result.";
    private TranscriptionMode _transcriptionMode = TranscriptionMode.Auto; private TranscriptionQuantization _quantization; private ComputeDevicePreference _device = ComputeDevicePreference.Auto;
    private decimal _confidenceThreshold = 0.60m;

    public AudioLabViewModel(IAudioImportService import, IWaveformGenerator waveforms, IWaveformCache cache,
        IBackgroundJobManager jobs, IAudioDeviceProvider devices, AudioPlaybackEngine playback,
        IUndoRedoService history, AudioStemMixPreview stemMix, IAudioIntelligenceWorkflow intelligence, IAudioSeparationEngine separationEngine,
        IAudioTranscriptionEngine transcriptionEngine, ILogger<AudioLabViewModel> logger)
    { _import = import; _waveforms = waveforms; _cache = cache; _jobs = jobs; _devices = devices; _playback = playback; _history = history;
        _stemMix = stemMix; _intelligence = intelligence; _separationEngine = separationEngine; _transcriptionEngine = transcriptionEngine; _logger = logger; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ProjectChanged;
    public VoidNoteProject Project { get; private set; } = new();
    public IReadOnlyList<AudioTrackRowViewModel> Tracks { get => _tracks; private set => Set(ref _tracks, value); }
    public AudioTrackRowViewModel? SelectedTrack { get => _selected; set { if (Set(ref _selected, value)) _ = LoadWaveformAsync(); } }
    public IReadOnlyList<StemRowViewModel> Stems { get => _stems; private set => Set(ref _stems, value); }
    public StemRowViewModel? SelectedStem { get => _selectedStem; set => Set(ref _selectedStem, value); }
    public WaveformData? Waveform { get => _waveform; private set { if (Set(ref _waveform, value)) OnPropertyChanged(nameof(WaveformWidth)); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public double Zoom { get => _zoom; set { if (Set(ref _zoom, Math.Clamp(value, 0.25, 16))) OnPropertyChanged(nameof(WaveformWidth)); } }
    public double WaveformWidth => Math.Max(900, 900 * Zoom);
    public double PlayheadSeconds { get => _playhead; set => Set(ref _playhead, Math.Clamp(value, 0, DurationSeconds)); }
    public double SelectionStartSeconds { get => _selectionStart; set { Set(ref _selectionStart, Math.Clamp(value, 0, DurationSeconds)); UpdateRegion(); } }
    public double SelectionEndSeconds { get => _selectionEnd; set { Set(ref _selectionEnd, Math.Clamp(value, 0, DurationSeconds)); UpdateRegion(); } }
    public bool LoopEnabled { get => Region.LoopEnabled; set { Region.LoopEnabled = value; OnPropertyChanged(); } }
    public double DurationSeconds => SelectedTrack?.Source.Format.Duration.Seconds is decimal value ? (double)value : 0;
    public string DeviceCapability => $"{_devices.Capability.Backend}: {_devices.Capability.Description}";
    public AudioRegion Region { get; } = new() { Name = "Audio Lab selection" };
    public string EngineStatus { get => _engineStatus; private set => Set(ref _engineStatus, value); }
    public string TranscriptionMetrics { get => _transcriptionMetrics; private set => Set(ref _transcriptionMetrics, value); }
    public IReadOnlyList<TranscriptionMode> TranscriptionModes { get; } = Enum.GetValues<TranscriptionMode>();
    public IReadOnlyList<TranscriptionQuantization> Quantizations { get; } = Enum.GetValues<TranscriptionQuantization>();
    public IReadOnlyList<ComputeDevicePreference> ComputeDevices { get; } = Enum.GetValues<ComputeDevicePreference>();
    public IReadOnlyList<string> SeparationEngines => [_separationEngine.Id];
    public IReadOnlyList<string> TranscriptionEngines => [_transcriptionEngine.Id];
    public string SelectedSeparationEngine => _separationEngine.Id;
    public string SelectedTranscriptionEngine => _transcriptionEngine.Id;
    public TranscriptionMode TranscriptionMode { get => _transcriptionMode; set => Set(ref _transcriptionMode, value); }
    public TranscriptionQuantization Quantization { get => _quantization; set => Set(ref _quantization, value); }
    public ComputeDevicePreference ComputeDevice { get => _device; set => Set(ref _device, value); }
    public decimal ConfidenceThreshold { get => _confidenceThreshold; set => Set(ref _confidenceThreshold, Math.Clamp(value, 0, 1)); }

    public async Task ImportAsync(string path, CancellationToken token = default)
    {
        CancelOperation(); _operation = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            IProgress<AudioImportProgress> progress = new Progress<AudioImportProgress>(value => { Progress = value.Fraction; Status = value.Stage; });
            var result = await _jobs.RunAsync("Audio probe/import", async (jobProgress, cancellation) =>
            {
                var combined = new InlineProgress<AudioImportProgress>(value => { progress.Report(value); jobProgress.Report(new(value.Fraction, value.Stage)); });
                return await _import.ImportAsync(Project, path, new(ProjectPathKind.Absolute), combined, cancellation);
            }, _operation.Token);
            Tracks = Project.AudioTracks.Select(track => new AudioTrackRowViewModel(Project, track, _history, RefreshTracks)).ToArray();
            SelectedTrack = Tracks.Single(row => row.Model.Id == result.Track.Id); Status = $"Imported {result.Source.Format.Codec} audio.";
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { Status = "Audio import cancelled."; }
        catch (Exception exception) { _logger.LogError(exception, "Audio import failed"); Status = exception.Message; }
    }

    public async Task DiscoverEnginesAsync(CancellationToken token = default)
    {
        var separation = await _separationEngine.DiscoverAsync(token); var transcription = await _transcriptionEngine.DiscoverAsync(token);
        EngineStatus = $"Separation: {separation.State} {separation.Version ?? string.Empty} [{separation.ExecutablePath ?? "no environment path"}] ({(separation.Capabilities.IsGpuAvailable ? "GPU available" : "CPU")}) Â· Transcription: {transcription.State} {transcription.Version ?? string.Empty} [{transcription.ExecutablePath ?? "no environment path"}]";
    }

    public async Task SeparateAsync(CancellationToken token = default)
    {
        if (SelectedTrack is null) { Status = "Select an original audio source first."; return; }
        CancelOperation(); _operation = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var regionId = PrepareRegion();
            IProgress<AudioIntelligenceProgress> progress = new Progress<AudioIntelligenceProgress>(value => { Progress = value.Fraction; Status = $"{value.Stage}: {value.Message}"; });
            var set = await _jobs.RunAsync("Stem separation", async (jobProgress, cancellation) =>
                await _intelligence.SeparateAsync(Project, SelectedTrack.Source.Id, regionId, new("htdemucs", ComputeDevice),
                    new InlineProgress<AudioIntelligenceProgress>(value => { progress.Report(value); jobProgress.Report(new(value.Fraction, value.Stage.ToString())); }), cancellation), _operation.Token);
            RefreshTracks(); Stems = Project.StemSets.SelectMany(value => value.StemTracks).Select(value => new StemRowViewModel(value)).ToArray();
            SelectedStem = Stems.FirstOrDefault(value => value.Model.Type == StemType.Bass) ?? Stems.FirstOrDefault();
            Status = $"Created {set.StemTracks.Count} non-destructive stems.";
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { Status = "Stem separation cancelled."; }
        catch (Exception exception) { _logger.LogError(exception, "Stem separation failed"); Status = exception.Message; }
    }

    public async Task TranscribeAsync(CancellationToken token = default)
    {
        if (SelectedStem is null && SelectedTrack is null) { Status = "Select audio or a stem first."; return; }
        CancelOperation(); _operation = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var sourceId = SelectedStem?.Model.AudioSourceId ?? SelectedTrack!.Source.Id;
            var stemId = SelectedStem?.Model.Id; var regionId = stemId.HasValue ? null : PrepareRegion();
            var settings = new AudioTranscriptionSettings
            {
                Mode = TranscriptionMode, MediumConfidenceThreshold = ConfidenceThreshold,
                HighConfidenceThreshold = Math.Max(0.85m, ConfidenceThreshold), ConfidenceFilter = ConfidenceFilterMode.MinimumThreshold,
                MinimumConfidence = ConfidenceThreshold, Quantization = Quantization, RemoveGhostNotes = true,
                MergeAdjacentNotes = true, DetectDuplicates = true, MarkPitchOutliers = true,
            };
            IProgress<AudioIntelligenceProgress> progress = new Progress<AudioIntelligenceProgress>(value => { Progress = value.Fraction; Status = $"{value.Stage}: {value.Message}"; });
            var result = await _jobs.RunAsync("Audio to MIDI", async (jobProgress, cancellation) =>
                await _intelligence.TranscribeAsync(Project, sourceId, stemId, regionId, new(settings, ComputeDevice),
                    new InlineProgress<AudioIntelligenceProgress>(value => { progress.Report(value); jobProgress.Report(new(value.Fraction, value.Stage.ToString())); }), cancellation), _operation.Token);
            var report = result.Report;
            TranscriptionMetrics = $"{report.KeptNotes}/{report.DetectedNotes} notes Â· avg confidence {report.AverageConfidence:P0} Â· high {report.HighConfidenceCount} / medium {report.MediumConfidenceCount} / low {report.LowConfidenceCount} Â· density {report.NoteDensityPerSecond:F2}/s";
            Status = $"Created editable MIDI track '{result.Track.Name}'.";
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { Status = "Audio transcription cancelled."; }
        catch (Exception exception) { _logger.LogError(exception, "Audio transcription failed"); Status = exception.Message; }
    }

    public void CompareOriginal()
    {
        var derived = Project.StemSets.SelectMany(value => value.StemTracks).Select(value => value.AudioSourceId).ToHashSet();
        SelectedTrack = Tracks.FirstOrDefault(value => !derived.Contains(value.Source.Id));
    }

    public void CompareStem()
    {
        if (SelectedStem is null) return;
        SelectedTrack = Tracks.FirstOrDefault(value => value.Source.Id == SelectedStem.Model.AudioSourceId);
    }

    public void RemoveSelectedStemSet()
    {
        if (SelectedStem is null) return;
        var set = Project.StemSets.Single(value => value.Id == SelectedStem.Model.StemSetId);
        _history.Execute(new RemoveStemSetCommand(Project, set));
        RefreshTracks(); Stems = Project.StemSets.SelectMany(value => value.StemTracks).Select(value => new StemRowViewModel(value)).ToArray();
        SelectedStem = Stems.FirstOrDefault(); Status = "Stem set removed from the project; Undo can restore it.";
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PlayStemMixAsync(CancellationToken token = default)
    {
        var sourceIds = Project.StemSets.SelectMany(value => value.StemTracks).Select(value => value.AudioSourceId).ToHashSet();
        var tracks = Project.AudioTracks.Where(track => track.Clips.Any(clip => sourceIds.Contains(clip.SourceId))).ToArray();
        try { _ = ObserveStemMixAsync(_stemMix.PlayAsync(Project, tracks, token)); Status = "Playing audible stem combination (preview synchronization)."; }
        catch (Exception exception) { Status = exception.Message; }
        await Task.CompletedTask;
    }

    private async Task ObserveStemMixAsync(Task run) { try { await run; Status = "Stem mix playback complete."; } catch (OperationCanceledException) { } catch (Exception exception) { _logger.LogError(exception, "Stem mix playback failed"); Status = exception.Message; } }

    public async Task LoadWaveformAsync(CancellationToken token = default)
    {
        if (SelectedTrack is null) { Waveform = null; return; } CancelOperation(); _operation = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var path = SelectedTrack.Source.ResolvedPath ?? SelectedTrack.Source.SourcePath;
            IProgress<double> progress = new Progress<double>(value => { Progress = value; Status = $"Generating waveform {value:P0}"; });
            Waveform = await _jobs.RunAsync("Waveform cache", async (jobProgress, cancellation) =>
            {
                var combined = new InlineProgress<double>(value => { progress.Report(value); jobProgress.Report(new(value, "Waveform generation")); });
                return await _waveforms.GetOrCreateAsync(path, combined, cancellation);
            }, _operation.Token);
            Progress = 1; Status = "Waveform ready."; OnPropertyChanged(nameof(DurationSeconds));
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError(exception, "Waveform generation failed"); Status = exception.Message; }
    }

    public async Task StartPlaybackAsync(CancellationToken token = default)
    {
        if (SelectedTrack is null) { Status = "Select an audio track first."; return; }
        try
        {
            var device = await _devices.OpenDefaultAsync(token); await _playback.LoadAsync(Project, SelectedTrack.Model, device, LoopEnabled ? Region : null, token);
            if (PlayheadSeconds > 0) await _playback.SeekAsync(new AbsoluteTime((decimal)PlayheadSeconds), token);
            _ = ObservePlaybackAsync(_playback.PlayAsync(token)); Status = "Playing audio on the master timeline.";
        }
        catch (Exception exception) { _logger.LogError(exception, "Audio playback could not start"); Status = exception.Message; }
    }

    public async Task PauseAsync(CancellationToken token = default) { await _playback.PauseAsync(token); RefreshPlaybackPosition(); Status = "Audio paused."; }
    public async Task StopAsync(CancellationToken token = default) { await _playback.StopAsync(token); RefreshPlaybackPosition(); Status = "Audio stopped."; }
    public async Task SeekAsync(CancellationToken token = default) { await _playback.SeekAsync(new AbsoluteTime((decimal)PlayheadSeconds), token); }
    public async Task ClearCacheAsync(CancellationToken token = default) { await _cache.ClearAsync(token); Waveform = null; Status = "Waveform cache cleared."; }
    public void ZoomIn() => Zoom *= 1.5;
    public void ZoomOut() => Zoom /= 1.5;
    public void CancelOperation() { _operation?.Cancel(); _operation?.Dispose(); _operation = null; }
    public void RefreshPlaybackPosition() { if (_playback.State != AudioPlaybackState.Stopped || _playback.Position.Seconds > 0) PlayheadSeconds = (double)_playback.Position.Seconds; }
    public void LoadProject(VoidNoteProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        CancelOperation(); Project = project; Waveform = null;
        Tracks = project.AudioTracks.Select(track => new AudioTrackRowViewModel(project, track, _history, RefreshTracks)).ToArray();
        SelectedTrack = Tracks.FirstOrDefault();
        Stems = project.StemSets.SelectMany(value => value.StemTracks).Select(value => new StemRowViewModel(value)).ToArray();
        SelectedStem = Stems.FirstOrDefault();
        Status = Tracks.Count == 0 ? "No audio tracks in this project." : $"Loaded {Tracks.Count} audio track(s).";
    }
    private void UpdateRegion() { var start = Math.Min(SelectionStartSeconds, SelectionEndSeconds); var end = Math.Max(SelectionStartSeconds, SelectionEndSeconds); Region.Start = new((decimal)start); Region.End = new((decimal)end); }
    private Guid? PrepareRegion()
    {
        UpdateRegion(); if (Region.Duration.Seconds <= 0) return null;
        if (!Project.AudioRegions.Contains(Region)) Project.AudioRegions.Add(Region); return Region.Id;
    }
    private void RefreshTracks() { Tracks = Project.AudioTracks.Select(track => new AudioTrackRowViewModel(Project, track, _history, RefreshTracks)).ToArray(); SelectedTrack = Tracks.FirstOrDefault(); ProjectChanged?.Invoke(this, EventArgs.Empty); }
    private async Task ObservePlaybackAsync(Task run) { try { await run; Status = "Audio playback complete."; } catch (Exception exception) { _logger.LogError(exception, "Audio playback failed"); Status = exception.Message; } }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T> { public void Report(T value) => callback(value); }
    public async Task ShutdownAsync() { CancelOperation(); await _stemMix.StopAsync(); await _playback.StopAsync(); }
    public async ValueTask DisposeAsync() { CancelOperation(); await _stemMix.DisposeAsync(); await _playback.DisposeAsync(); }
}

public sealed record StemRowViewModel(Stem Model)
{
    public string Name => Model.Name;
    public string Details => $"{Model.Type} Â· {Model.Engine} {Model.EngineVersion} Â· offset {Model.StartOffset.Seconds:F2}s Â· duration {Model.Duration.Seconds:F2}s";
}

public sealed class AudioTrackRowViewModel : INotifyPropertyChanged
{
    private readonly VoidNoteProject _project; private readonly IUndoRedoService _history; private readonly Action _changed;
    public AudioTrackRowViewModel(VoidNoteProject project, AudioTrack track, IUndoRedoService history, Action changed)
    { _project = project; Model = track; _history = history; _changed = changed; Source = project.AudioSources.Single(value => value.Id == track.Clips[0].SourceId); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public AudioTrack Model { get; }
    public AudioSource Source { get; }
    public string Name { get => Model.Name; set => Change("Rename audio track", current => Model.Name = current, Model.Name, value); }
    public decimal Gain { get => Model.Gain; set => Change("Change audio gain", current => Model.Gain = current, Model.Gain, Math.Clamp(value, 0, 2)); }
    public bool IsMuted { get => Model.IsMuted; set => Change("Mute audio track", current => Model.IsMuted = current, Model.IsMuted, value); }
    public bool IsSolo { get => Model.IsSolo; set => Change("Solo audio track", current => Model.IsSolo = current, Model.IsSolo, value); }
    public bool IsEnabled { get => Model.IsEnabled; set => Change("Enable audio track", current => Model.IsEnabled = current, Model.IsEnabled, value); }
    public double StartSeconds { get => (double)_project.Timeline.ToAbsoluteTime(Model.Clips[0].Start).Seconds; set { var before = Model.Clips[0].Start; var after = _project.Timeline.ToMusicalTime(new AbsoluteTime((decimal)Math.Max(0, value))); Change("Move audio track", current => Model.Clips[0].Start = current, before, after); } }
    public string Format => Source.Format.Container;
    public string Details => $"{Source.Format.Codec} · {Source.Format.SampleRate:N0} Hz · {Source.Format.ChannelCount} ch · {Source.Format.Duration.Seconds:F2} s";
    public void Remove() { _history.Execute(new RemoveAudioTrackCommand(_project, Model)); _changed(); }
    private void Change<T>(string description, Action<T> setter, T before, T after) { if (EqualityComparer<T>.Default.Equals(before, after)) return; _history.Execute(new SetAudioTrackValueCommand<T>(description, setter, before, after)); PropertyChanged?.Invoke(this, new(null)); }
}
