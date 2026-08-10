using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Mandachord.Generation;
using VoidNote.Mandachord.Mapping;

namespace VoidNote.Mandachord.Tests;

public sealed class GeneratorTests
{
    private readonly MandachordGenerator _generator = new(new MandachordPitchMapper(), new MandachordTimingMapper());
    [Theory]
    [InlineData(MandachordGenerationPreset.Faithful)] [InlineData(MandachordGenerationPreset.Recognizable)] [InlineData(MandachordGenerationPreset.Gameplay)]
    [InlineData(MandachordGenerationPreset.RhythmFocus)] [InlineData(MandachordGenerationPreset.MelodyFocus)]
    public void Presets_CreateThreeValidDeterministicCandidates(MandachordGenerationPreset preset)
    {
        var first = _generator.Generate(Timeline(), [Source()], preset, new()); var second = _generator.Generate(Timeline(), [Source()], preset, new());
        Assert.Equal(3, first.Candidates.Count); Assert.Equal(first.Candidates.Select(value => value.Arrangement.Id), second.Candidates.Select(value => value.Arrangement.Id));
        Assert.All(first.Candidates, candidate => { candidate.Arrangement.Validate(); Assert.InRange(candidate.Report.Scores.Similarity, 0, 100); });
    }
    [Fact] public void MelodyReduction_UsesImportanceContinuityRepetitionAndReportsDrops()
    {
        var result = _generator.Generate(Timeline(), [Source(polyphonic: true)], MandachordGenerationPreset.MelodyFocus, new() { MaximumLayerDensity = 0.2m }); var report = result.Candidates.Single(value => value.Arrangement.Preset == MandachordGenerationPreset.MelodyFocus).Report;
        Assert.True(report.DroppedNotes > 0); Assert.Contains(report.Changes, value => value.Type is MandachordChangeType.CollisionResolved or MandachordChangeType.Dropped); Assert.True(report.Scores.MelodyPreservation > 0);
    }
    [Fact] public void BassReduction_IsNotSimplyLowestNote()
    {
        var notes = new[] { Note(0, 36, 60, 20), Note(0, 48, 960, 120), Note(240, 48, 960, 120), Note(480, 48, 960, 120) };
        var source = new MandachordSourceTrack(Guid.Parse("00000000-0000-0000-0000-000000000010"), "Bass", MandachordSourceKind.MidiTrack, notes, MandachordLayer.Bass);
        var pattern = _generator.Generate(Timeline(), [source], MandachordGenerationPreset.Faithful, new()).Candidates.First().Arrangement.Patterns[0];
        Assert.Contains(pattern.Steps, value => value.Layer == MandachordLayer.Bass && value.PitchPosition == 4); // repeated C wins over isolated lower outlier
    }
    [Fact] public void Percussion_UsesAnalysisMetadataWhenAvailable()
    {
        var source = Source() with { RhythmEvents = [new(new(0), MandachordPercussionCategory.Kick, 1m), new(new(480), MandachordPercussionCategory.Snare, 0.8m)] };
        var result = _generator.Generate(Timeline(), [source], MandachordGenerationPreset.RhythmFocus, new()).Candidates.First();
        Assert.Contains(result.Arrangement.Patterns[0].Steps, value => value.PercussionCategory == MandachordPercussionCategory.Kick); Assert.Contains(result.Report.Changes, value => value.Reason.Contains("rhythm-analysis"));
    }
    [Fact] public void PercussionFallback_UsesOnsetsAndDoesNotInventSourceMidiDrumPitches()
    {
        var result = _generator.Generate(Timeline(), [Source()], MandachordGenerationPreset.RhythmFocus, new()).Candidates.First();
        Assert.Contains(result.Report.Changes, value => value.Reason.Contains("no pitched drum notes")); Assert.All(result.Arrangement.Patterns[0].Steps.Where(value => value.Layer == MandachordLayer.Percussion), value => Assert.Null(value.PitchPosition));
    }
    [Fact] public void CandidateRanking_IsSequentialAndRequestedScoreDriven()
    {
        var result = _generator.Generate(Timeline(), [Source()], MandachordGenerationPreset.Gameplay, new()); Assert.Equal([1, 2, 3], result.Candidates.Select(value => value.Rank)); Assert.Equal(result.Candidates.Max(value => value.Report.Scores.Gameplay), result.Candidates[0].Report.Scores.Gameplay);
    }
    [Fact] public void ScoringFormula_IsBoundedAndExplainable()
    {
        var result = _generator.Generate(Timeline(), [Source()], MandachordGenerationPreset.Faithful, new()).Candidates.First(); var scores = result.Report.Scores;
        Assert.All(new[] { scores.Similarity, scores.MelodyPreservation, scores.RhythmMatch, scores.BassPreservation, scores.Gameplay, scores.Density }, value => Assert.InRange(value, 0m, 100m)); Assert.NotEmpty(result.Report.Changes);
    }
    private static ProjectTimeline Timeline() => ProjectTimeline.CreateDefault();
    private static MandachordSourceTrack Source(bool polyphonic = false)
    {
        var notes = Enumerable.Range(0, 16).Select(i => Note(i * 240, 62 + new[] { 0, 3, 5, 7, 10 }[i % 5], 240, 80 + i)).ToList();
        notes.AddRange(Enumerable.Range(0, 8).Select(i => Note(i * 480, 38 + new[] { 0, 3, 5, 7, 10 }[i % 5], 480, 90)));
        if (polyphonic) notes.AddRange(Enumerable.Range(0, 16).Select(i => Note(i * 240, 74, 120, 30)));
        return new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Fixture", MandachordSourceKind.MidiTrack, notes);
    }
    private static MusicalEvent Note(long start, int pitch, long duration = 240, int velocity = 100) => new(GuidFrom(start, pitch, duration), new(start), new(duration), pitch, velocity, MusicalEventSource.ImportedMidi, 1m);
    private static Guid GuidFrom(long start, int pitch, long duration) { Span<byte> bytes = stackalloc byte[16]; BitConverter.TryWriteBytes(bytes, start); BitConverter.TryWriteBytes(bytes[8..], pitch); BitConverter.TryWriteBytes(bytes[12..], (int)duration); return new(bytes); }
}
