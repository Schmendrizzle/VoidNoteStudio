using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;

namespace VoidNote.Mandachord.Mapping;

public enum MandachordPitchMappingKind { Exact, OctaveShift, TranspositionPreferred, NotMeaningful }
public sealed record MandachordPitchCandidate(MandachordPitchMappingKind Kind, int SourcePitch, MandachordPitch? Pitch, int SemitoneChange, int SuggestedTransposition, string Reason);

public interface IMandachordPitchMapper
{
    MandachordPitchCandidate Map(int midiPitch, MandachordLayer layer, int transposition = 0);
    IReadOnlyList<MandachordPitchCandidate> FindTranspositions(IEnumerable<int> midiPitches, MandachordLayer layer);
}

public sealed class MandachordPitchMapper(MandachordGridDefinition? definition = null) : IMandachordPitchMapper
{
    private readonly MandachordGridDefinition _grid = definition ?? MandachordGridDefinition.Standard;

    public MandachordPitchCandidate Map(int midiPitch, MandachordLayer layer, int transposition = 0)
    {
        if (midiPitch is < 0 or > 127 || layer == MandachordLayer.Percussion) return new(MandachordPitchMappingKind.NotMeaningful, midiPitch, null, 0, 0, "Pitch is not a tonal Mandachord input.");
        var pitches = layer == MandachordLayer.Bass ? _grid.BassPitches : _grid.MelodyPitches;
        var shifted = midiPitch + transposition;
        var exact = pitches.FirstOrDefault(value => value.PreviewMidiPitch == shifted);
        if (exact is not null) return new(MandachordPitchMappingKind.Exact, midiPitch, exact, transposition, transposition, "Exact VoidNote preview pitch.");
        var octave = pitches.Where(value => value.PitchClass == Mod(shifted, 12)).OrderBy(value => Math.Abs(value.PreviewMidiPitch - shifted)).ThenBy(value => value.Position).FirstOrDefault();
        if (octave is not null) return new(MandachordPitchMappingKind.OctaveShift, midiPitch, octave, octave.PreviewMidiPitch - midiPitch, transposition, "Pitch class is available through octave displacement.");
        var closest = pitches.OrderBy(value => Math.Abs(value.PreviewMidiPitch - shifted)).ThenBy(value => value.PreviewMidiPitch).First();
        var delta = closest.PreviewMidiPitch - shifted;
        if (Math.Abs(delta) <= 2) return new(MandachordPitchMappingKind.TranspositionPreferred, midiPitch, closest, closest.PreviewMidiPitch - midiPitch, transposition + delta, "A small global transposition improves representation.");
        return new(MandachordPitchMappingKind.NotMeaningful, midiPitch, null, 0, 0, "Nearest Mandachord pitch is more than two semitones away.");
    }

    public IReadOnlyList<MandachordPitchCandidate> FindTranspositions(IEnumerable<int> midiPitches, MandachordLayer layer) =>
        Enumerable.Range(-6, 13).Select(shift =>
        {
            var mapped = midiPitches.Select(value => Map(value, layer, shift)).ToArray();
            var represented = mapped.Count(value => value.Pitch is not null);
            var distance = mapped.Where(value => value.Pitch is not null).Sum(value => Math.Abs(value.SemitoneChange));
            return (Candidate: new MandachordPitchCandidate(represented == mapped.Length ? MandachordPitchMappingKind.TranspositionPreferred : MandachordPitchMappingKind.NotMeaningful,
                0, null, distance, shift, $"{represented}/{mapped.Length} represented; total pitch distance {distance}."), Represented: represented);
        }).OrderByDescending(value => value.Represented).ThenBy(value => value.Candidate.SemitoneChange).ThenBy(value => Math.Abs(value.Candidate.SuggestedTransposition)).ThenBy(value => value.Candidate.SuggestedTransposition).Select(value => value.Candidate).ToArray();

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}

public sealed record MandachordTimingMapping(Guid SourceEventId, int StepIndex, decimal ExactStep, decimal TimingErrorSteps, MusicalTime QuantizedTime, bool Collision);

public interface IMandachordTimingMapper
{
    IReadOnlyList<MandachordTimingMapping> Map(ProjectTimeline timeline, IEnumerable<MusicalEvent> events, MusicalTime loopStart);
}

public sealed class MandachordTimingMapper(MandachordGridDefinition? definition = null) : IMandachordTimingMapper
{
    private readonly MandachordGridDefinition _grid = definition ?? MandachordGridDefinition.Standard;
    public IReadOnlyList<MandachordTimingMapping> Map(ProjectTimeline timeline, IEnumerable<MusicalEvent> events, MusicalTime loopStart)
    {
        var mapped = events.OrderBy(value => value.StartTime.Ticks).ThenBy(value => value.Pitch).ThenBy(value => value.Id).Select(value =>
        {
            var relativeBeats = timeline.ToBeats(value.StartTime) - timeline.ToBeats(loopStart);
            var exact = relativeBeats * _grid.StepsPerBeat;
            var absoluteStep = decimal.ToInt64(decimal.Round(exact, 0, MidpointRounding.AwayFromZero));
            var wrapped = (int)((absoluteStep % _grid.StepCount + _grid.StepCount) % _grid.StepCount);
            var quantized = timeline.FromBeats(timeline.ToBeats(loopStart) + absoluteStep / (decimal)_grid.StepsPerBeat);
            return new MandachordTimingMapping(value.Id, wrapped, exact, absoluteStep - exact, quantized, false);
        }).ToArray();
        var collisions = mapped.GroupBy(value => value.StepIndex).Where(value => value.Count() > 1).SelectMany(value => value.Select(item => item.SourceEventId)).ToHashSet();
        return mapped.Select(value => value with { Collision = collisions.Contains(value.SourceEventId) }).ToArray();
    }
}
