using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoidNote.Application.Mandachord;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Mandachord.Generation;
using VoidNote.Mandachord.Preview;

namespace VoidNote.App.ViewModels;

public sealed class MandachordStudioViewModel(IMandachordGenerator generator, IMandachordPreviewRenderer preview, IMandachordEditorService editor) : INotifyPropertyChanged
{
    private ProjectTimeline _timeline = ProjectTimeline.CreateDefault(); private MidiTrack? _source; private MandachordGenerationPreset _preset;
    private int _editStep; private int _editPitch;
    private IReadOnlyList<MandachordCandidateViewModel> _candidates = []; private MandachordCandidateViewModel? _selected; private string _status = "Load a MIDI source to begin."; private byte[]? _preview;
    public event PropertyChangedEventHandler? PropertyChanged;
    public VoidNoteProject Project { get; private set; } = new() { Metadata = new() { Title = "Mandachord Studio" } };
    public IReadOnlyList<MidiTrack> Sources { get; private set; } = [];
    public MidiTrack? SelectedSource { get => _source; set => Set(ref _source, value); }
    public IReadOnlyList<MandachordGenerationPreset> Presets { get; } = Enum.GetValues<MandachordGenerationPreset>();
    public MandachordGenerationPreset SelectedPreset { get => _preset; set => Set(ref _preset, value); }
    public IReadOnlyList<MandachordCandidateViewModel> Candidates { get => _candidates; private set { _candidates = value; Notify(); } }
    public MandachordCandidateViewModel? SelectedCandidate { get => _selected; set { if (Set(ref _selected, value)) NotifyGrid(); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public IReadOnlyList<MandachordStep> PercussionSteps => Steps(MandachordLayer.Percussion);
    public IReadOnlyList<MandachordStep> BassSteps => Steps(MandachordLayer.Bass);
    public IReadOnlyList<MandachordStep> MelodySteps => Steps(MandachordLayer.Melody);
    public bool HasPreview => _preview is { Length: > 44 };
    public int EditStep { get => _editStep; set => Set(ref _editStep, Math.Clamp(value, 0, 63)); }
    public int EditPitch { get => _editPitch; set => Set(ref _editPitch, Math.Clamp(value, 0, 4)); }

    public void Prepare(ProjectTimeline timeline, IReadOnlyList<MidiTrack> tracks)
    {
        _timeline = timeline; Sources = tracks; SelectedSource = tracks.FirstOrDefault(); Project = new() { Metadata = new() { Title = "Mandachord Studio" }, Timeline = timeline, MidiTracks = [.. tracks], MandachordSoundSets = [BuiltInMandachordSoundSets.SyntheticDefault()] }; Notify(nameof(Sources)); Status = $"{tracks.Count} source track(s) available.";
    }
    public void Generate()
    {
        if (SelectedSource is null) { Status = "Select a normalized source track first."; return; }
        var source = new MandachordSourceTrack(SelectedSource.Id, SelectedSource.Name, SelectedSource.Events.Any(value => value.AudioProvenance is not null) ? MandachordSourceKind.AudioTranscriptionTrack : MandachordSourceKind.MidiTrack, SelectedSource.Events);
        var result = generator.Generate(_timeline, [source], SelectedPreset, new() { CandidateCount = 3, SoundSetId = Project.MandachordSoundSets[0].Id });
        Candidates = result.Candidates.Select(value => new MandachordCandidateViewModel(value)).ToArray(); SelectedCandidate = Candidates.FirstOrDefault(); Status = $"Generated {Candidates.Count} deterministic candidates.";
    }
    public void Accept() { if (SelectedCandidate is null) return; editor.AcceptCandidate(Project, SelectedCandidate.Model.Arrangement); Status = "Candidate added to the project library; Undo can remove it."; }
    public void AddStep(MandachordLayer layer, int step, int pitch = 0, MandachordPercussionCategory percussion = MandachordPercussionCategory.Kick) { var pattern = Pattern(); if (pattern is null) return; editor.SetStep(pattern, layer, step, layer == MandachordLayer.Percussion ? null : pitch, layer == MandachordLayer.Percussion ? percussion : null); NotifyGrid(); }
    public void DeleteCell(MandachordLayer layer) { var pattern = Pattern(); if (pattern is null) return; var ids = pattern.Steps.Where(value => value.Layer == layer && value.StepIndex == EditStep).Select(value => value.Id).ToArray(); editor.DeleteSteps(pattern, ids); NotifyGrid(); }
    public void Clear() { var pattern = Pattern(); if (pattern is null) return; editor.Clear(pattern); NotifyGrid(); Status = "Grid cleared; Undo can restore it."; }
    public void Reset() { Candidates = []; SelectedCandidate = null; _preview = null; Notify(nameof(HasPreview)); NotifyGrid(); Status = "Candidates reset. Generate to start again."; }
    public void RenderPreview() { var pattern = Pattern(); if (pattern is null) return; _preview = preview.Render(pattern, Project.MandachordSoundSets[0]).WaveData; Notify(nameof(HasPreview)); Status = "Synthetic eight-second loop preview rendered."; }
    public async Task SavePreviewAsync(string path, CancellationToken token = default) { if (_preview is null) RenderPreview(); if (_preview is null) return; await File.WriteAllBytesAsync(path, _preview, token); Status = "Synthetic Mandachord WAV saved."; }
    private MandachordPattern? Pattern() => SelectedCandidate?.Model.Arrangement.Patterns.FirstOrDefault();
    private IReadOnlyList<MandachordStep> Steps(MandachordLayer layer) => Pattern()?.Steps.Where(value => value.Layer == layer).OrderBy(value => value.StepIndex).ToArray() ?? [];
    private void NotifyGrid() { Notify(nameof(PercussionSteps)); Notify(nameof(BassSteps)); Notify(nameof(MelodySteps)); }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Notify(name); return true; }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed record MandachordCandidateViewModel(MandachordGenerationCandidate Model)
{
    public string Name => Model.Arrangement.Name;
    public string Scores => $"Similarity {Model.Report.Scores.Similarity:0.##}% · Melody {Model.Report.Scores.MelodyPreservation:0.##}% · Rhythm {Model.Report.Scores.RhythmMatch:0.##}% · Bass {Model.Report.Scores.BassPreservation:0.##}% · Gameplay {Model.Report.Scores.Gameplay:0.##}% · Density {Model.Report.Scores.Density:0.##}%";
}
