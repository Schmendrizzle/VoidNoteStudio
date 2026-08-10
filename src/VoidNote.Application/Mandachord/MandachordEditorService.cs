using VoidNote.Application.Commands;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Projects;

namespace VoidNote.Application.Mandachord;

public interface IMandachordEditorService
{
    void SetStep(MandachordPattern pattern, MandachordLayer layer, int stepIndex, int? pitchPosition = null, MandachordPercussionCategory? percussion = null, int velocity = 100);
    void DeleteSteps(MandachordPattern pattern, IReadOnlyCollection<Guid> stepIds);
    void ChangePitch(MandachordPattern pattern, IReadOnlyCollection<Guid> stepIds, int pitchPosition);
    IReadOnlyList<MandachordStep> Copy(MandachordPattern pattern, IReadOnlyCollection<Guid> stepIds);
    void Paste(MandachordPattern pattern, IReadOnlyList<MandachordStep> clipboard, int offsetSteps);
    void Clear(MandachordPattern pattern, MandachordLayer? layer = null);
    void AcceptCandidate(VoidNoteProject project, MandachordArrangement candidate);
    void DeletePattern(MandachordArrangement arrangement, Guid patternId);
    void AssignSection(MandachordArrangement arrangement, Guid sectionId, Guid patternId);
    void ChangeSoundSet(MandachordArrangement arrangement, Guid soundSetId);
}

public sealed class MandachordEditorService(IUndoRedoService history) : IMandachordEditorService
{
    public void SetStep(MandachordPattern pattern, MandachordLayer layer, int stepIndex, int? pitchPosition = null, MandachordPercussionCategory? percussion = null, int velocity = 100)
    {
        if (stepIndex is < 0 or >= 64 || velocity is < 1 or > 127) throw new ArgumentOutOfRangeException(nameof(stepIndex));
        if (layer == MandachordLayer.Percussion && percussion is null || layer != MandachordLayer.Percussion && pitchPosition is < 0 or > 4) throw new ArgumentException("The grid position does not match its layer.");
        var existing = pattern.Steps.SingleOrDefault(value => value.Layer == layer && value.StepIndex == stepIndex && (layer != MandachordLayer.Percussion || value.PercussionCategory == percussion));
        if (existing is null)
        {
            var added = new MandachordStep { Name = $"{layer} {stepIndex + 1}", Layer = layer, StepIndex = stepIndex, PitchPosition = pitchPosition, PercussionCategory = percussion, Velocity = velocity, Provenance = new() { EditKind = MandachordStepEditKind.ManualAdded, ManualChanges = ["Added manually"] } };
            history.Execute(new ListCommand<MandachordStep>("Add Mandachord step", pattern.Steps, added, true, Touch(pattern)));
        }
        else
        {
            var oldPitch = existing.PitchPosition; var oldPercussion = existing.PercussionCategory; var oldVelocity = existing.Velocity;
            history.Execute(new DelegateCommand("Change Mandachord step", () => { existing.PitchPosition = pitchPosition; existing.PercussionCategory = percussion; existing.Velocity = velocity; Mark(existing, "Step changed manually"); TouchNow(pattern); }, () => { existing.PitchPosition = oldPitch; existing.PercussionCategory = oldPercussion; existing.Velocity = oldVelocity; TouchNow(pattern); }));
        }
    }

    public void DeleteSteps(MandachordPattern pattern, IReadOnlyCollection<Guid> stepIds) => Replace(pattern, pattern.Steps.Where(value => !stepIds.Contains(value.Id)).ToArray(), "Delete Mandachord steps");
    public void ChangePitch(MandachordPattern pattern, IReadOnlyCollection<Guid> stepIds, int pitchPosition)
    {
        if (pitchPosition is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(pitchPosition)); var targets = pattern.Steps.Where(value => stepIds.Contains(value.Id) && value.Layer != MandachordLayer.Percussion).ToArray(); var old = targets.Select(value => value.PitchPosition).ToArray();
        history.Execute(new DelegateCommand("Change Mandachord pitch", () => { foreach (var step in targets) { step.PitchPosition = pitchPosition; Mark(step, $"Pitch set to position {pitchPosition}"); } TouchNow(pattern); }, () => { for (var i = 0; i < targets.Length; i++) targets[i].PitchPosition = old[i]; TouchNow(pattern); }));
    }
    public IReadOnlyList<MandachordStep> Copy(MandachordPattern pattern, IReadOnlyCollection<Guid> stepIds) => pattern.Steps.Where(value => stepIds.Contains(value.Id)).OrderBy(value => value.StepIndex).Select(value => Clone(value)).ToArray();
    public void Paste(MandachordPattern pattern, IReadOnlyList<MandachordStep> clipboard, int offsetSteps)
    {
        var pasted = clipboard.Select(value => Clone(value, Math.Clamp(value.StepIndex + offsetSteps, 0, 63))).Where(value => !pattern.Steps.Any(existing => SameCell(existing, value))).ToArray();
        history.Execute(new BatchListCommand("Paste Mandachord steps", pattern.Steps, pasted, Touch(pattern)));
    }
    public void Clear(MandachordPattern pattern, MandachordLayer? layer = null) => Replace(pattern, pattern.Steps.Where(value => layer.HasValue && value.Layer != layer).ToArray(), "Clear Mandachord grid");
    public void AcceptCandidate(VoidNoteProject project, MandachordArrangement candidate) => history.Execute(new ListCommand<MandachordArrangement>("Accept Mandachord candidate", project.MandachordArrangements, candidate, true));
    public void DeletePattern(MandachordArrangement arrangement, Guid patternId)
    {
        if (arrangement.Sections.Any(value => value.PatternId == patternId)) throw new InvalidOperationException("Reassign sections before deleting their pattern."); var pattern = arrangement.Patterns.Single(value => value.Id == patternId); history.Execute(new ListCommand<MandachordPattern>("Delete Mandachord pattern", arrangement.Patterns, pattern, false));
    }
    public void AssignSection(MandachordArrangement arrangement, Guid sectionId, Guid patternId)
    {
        if (!arrangement.Patterns.Any(value => value.Id == patternId)) throw new InvalidOperationException("Pattern does not belong to arrangement."); var section = arrangement.Sections.Single(value => value.Id == sectionId); var old = section.PatternId; history.Execute(new DelegateCommand("Assign Mandachord section", () => section.PatternId = patternId, () => section.PatternId = old));
    }
    public void ChangeSoundSet(MandachordArrangement arrangement, Guid soundSetId) { var old = arrangement.SelectedSoundSetId; history.Execute(new DelegateCommand("Change Mandachord sound set", () => arrangement.SelectedSoundSetId = soundSetId, () => arrangement.SelectedSoundSetId = old)); }

    private void Replace(MandachordPattern pattern, IReadOnlyList<MandachordStep> replacement, string description) { var old = pattern.Steps.ToArray(); history.Execute(new DelegateCommand(description, () => { pattern.Steps.Clear(); pattern.Steps.AddRange(replacement); TouchNow(pattern); }, () => { pattern.Steps.Clear(); pattern.Steps.AddRange(old); TouchNow(pattern); })); }
    private static MandachordStep Clone(MandachordStep value, int? step = null) => new() { Name = value.Name, Layer = value.Layer, StepIndex = step ?? value.StepIndex, PitchPosition = value.PitchPosition, PercussionCategory = value.PercussionCategory, Velocity = value.Velocity, Provenance = value.Provenance with { EditKind = MandachordStepEditKind.ManualAdded, ManualChanges = [.. value.Provenance.ManualChanges, "Copied/pasted manually"] } };
    private static bool SameCell(MandachordStep left, MandachordStep right) => left.Layer == right.Layer && left.StepIndex == right.StepIndex && (left.Layer != MandachordLayer.Percussion || left.PercussionCategory == right.PercussionCategory);
    private static void Mark(MandachordStep step, string change) { step.Provenance.EditKind = MandachordStepEditKind.ManualModified; step.Provenance.ManualChanges.Add(change); }
    private static Action Touch(MandachordPattern pattern) => () => TouchNow(pattern);
    private static void TouchNow(MandachordPattern pattern) => pattern.ModifiedAt = DateTimeOffset.UtcNow;
    private sealed record DelegateCommand(string Description, Action Apply, Action Revert) : IUndoableCommand { public void Execute() => Apply(); public void Undo() => Revert(); }
    private sealed class ListCommand<T>(string description, IList<T> list, T item, bool adding, Action? changed = null) : IUndoableCommand { private int _index; public string Description => description; public void Execute() { if (adding) { _index = list.Count; list.Add(item); } else { _index = list.IndexOf(item); list.Remove(item); } changed?.Invoke(); } public void Undo() { if (adding) list.Remove(item); else list.Insert(Math.Max(0, _index), item); changed?.Invoke(); } }
    private sealed class BatchListCommand(string description, IList<MandachordStep> list, IReadOnlyList<MandachordStep> items, Action changed) : IUndoableCommand { public string Description => description; public void Execute() { foreach (var item in items) if (!list.Contains(item)) list.Add(item); changed(); } public void Undo() { foreach (var item in items) list.Remove(item); changed(); } }
}
