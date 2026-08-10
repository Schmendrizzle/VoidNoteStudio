using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoidNote.Application.Shawzin;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Definitions;

namespace VoidNote.App.ViewModels;

/// <summary>Presentation state for the minimal Milestone-D Shawzin Studio.</summary>
public sealed class MainWindowViewModel(IShawzinStudioWorkflow workflow) : INotifyPropertyChanged
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
        OnPropertyChanged(nameof(HasPreview));
        Status = result.Arrangement.IsSuccess
            ? $"Arranged {result.Arrangement.Report.OutputNoteCount} notes with {result.Arrangement.Report.Changes.Count} reported changes."
            : $"Arrangement has {result.Arrangement.Report.Changes.Count(value => value.ChangeType == ArrangementChangeType.ConflictUnresolved)} unresolved conflict(s).";
    }

    public async Task SavePreviewAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_previewWave is null) { Status = "Arrange a track before saving a preview."; return; }
        await File.WriteAllBytesAsync(path, _previewWave, cancellationToken);
        Status = "Synthetic WAV preview saved.";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
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
