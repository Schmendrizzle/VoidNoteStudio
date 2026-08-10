using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Definitions;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>Builds independently analyzed and arranged Shawzin tracks from split voices.</summary>
public interface IShawzinEnsembleArranger
{
    ShawzinEnsemble Arrange(MultiShawzinSplitResult split, ProjectTimeline timeline, string name = "Shawzin Ensemble");
    void RearrangeTrack(ShawzinEnsemble ensemble, ShawzinEnsembleTrack track);
}

/// <inheritdoc />
public sealed class ShawzinEnsembleArranger(
    IShawzinScaleAnalyzer scaleAnalyzer,
    IShawzinTranspositionAnalyzer transpositionAnalyzer,
    IShawzinCompatibilityAnalyzer compatibilityAnalyzer,
    IShawzinArranger arranger) : IShawzinEnsembleArranger
{
    public ShawzinEnsemble Arrange(MultiShawzinSplitResult split, ProjectTimeline timeline, string name = "Shawzin Ensemble")
    {
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(timeline);
        var ensemble = new ShawzinEnsemble
        {
            Id = StableEnsembleId(split),
            Name = name,
            MasterTimeline = timeline,
            SplitReport = split.Report,
        };
        foreach (var voice in split.Voices)
        {
            var preference = voice.Preference;
            var instrument = preference?.Instrument ?? BuiltInShawzinDefinitions.All[ensemble.Tracks.Count % BuiltInShawzinDefinitions.All.Count];
            var scaleCandidates = scaleAnalyzer.Analyze(voice.SourceTrack, instrument);
            var scale = preference?.Scale ?? scaleCandidates[0].Scale;
            var transpositions = transpositionAnalyzer.Analyze(voice.SourceTrack, timeline, instrument, scale);
            var transposition = preference?.TranspositionSemitones ?? transpositions[0].Semitones;
            var track = new ShawzinEnsembleTrack
            {
                Id = voice.Id,
                DisplayName = voice.DisplayName,
                Instrument = instrument,
                Scale = scale,
                TranspositionSemitones = transposition,
                ArrangementStrategies = preference?.ArrangementStrategies ?? ArrangementStrategy.ClosestPitch | ArrangementStrategy.OctaveShift |
                    ArrangementStrategy.PreserveMelody | ArrangementStrategy.Arpeggiate,
                SourceTrack = voice.SourceTrack,
                ScaleCandidates = scaleCandidates,
                TranspositionCandidates = transpositions,
            };
            ensemble.Tracks.Add(track);
            RearrangeTrack(ensemble, track, preference?.ArrangementStrategies);
        }
        ensemble.SplitReport = ensemble.SplitReport with
        {
            LaterArrangementChanges = ensemble.Tracks.SelectMany(value => value.ArrangementReport?.Changes ?? []).ToArray(),
        };
        ensemble.OptimizationReport = EnsembleOptimizer.Evaluate(ensemble);
        return ensemble;
    }

    public void RearrangeTrack(ShawzinEnsemble ensemble, ShawzinEnsembleTrack track) => RearrangeTrack(ensemble, track, null);

    private void RearrangeTrack(ShawzinEnsemble ensemble, ShawzinEnsembleTrack track, ArrangementStrategy? preferred)
    {
        ArgumentNullException.ThrowIfNull(ensemble);
        ArgumentNullException.ThrowIfNull(track);
        track.ScaleCandidates = scaleAnalyzer.Analyze(track.SourceTrack, track.Instrument);
        track.TranspositionCandidates = transpositionAnalyzer.Analyze(track.SourceTrack, ensemble.MasterTimeline, track.Instrument, track.Scale);
        track.Compatibility = compatibilityAnalyzer.Analyze(Transposed(track.SourceTrack, track.TranspositionSemitones), ensemble.MasterTimeline, track.Instrument, track.Scale);
        var options = new ArrangementOptions
        {
            Scale = track.Scale,
            Strategies = preferred ?? track.ArrangementStrategies,
            AllowTransposition = track.TranspositionSemitones != 0,
            TranspositionSemitones = track.TranspositionSemitones,
        };
        var result = arranger.Arrange(track.SourceTrack, ensemble.MasterTimeline, track.Instrument, options);
        track.ShawzinTrack = result.Track;
        track.ArrangementReport = result.Report;
        ensemble.SplitReport = ensemble.SplitReport with
        {
            LaterArrangementChanges = ensemble.Tracks.SelectMany(value => value.ArrangementReport?.Changes ?? []).ToArray(),
        };
        ensemble.OptimizationReport = EnsembleOptimizer.Evaluate(ensemble);
    }

    private static MidiTrack Transposed(MidiTrack source, int semitones) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Events = source.Events.Where(value => value.Pitch + semitones is >= 0 and <= 127).Select(value => new MusicalEvent(
            value.Id, value.StartTime, value.Duration, value.Pitch + semitones, value.Velocity, value.Source, value.Confidence)).ToList(),
    };

    private static Guid StableEnsembleId(MultiShawzinSplitResult split)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join(',', split.Voices.Select(value => value.Id)) + '|' + split.Report.Strategy));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

/// <summary>Calculates explainable ensemble-level quality metrics.</summary>
public static class EnsembleOptimizer
{
    public static EnsembleOptimizationReport Evaluate(ShawzinEnsemble ensemble)
    {
        ArgumentNullException.ThrowIfNull(ensemble);
        var source = ensemble.SplitReport.Metrics.SourceNoteCount;
        var arranged = ensemble.Tracks.Sum(value => value.ArrangementReport?.OutputNoteCount ?? 0);
        var arrangementDrops = Math.Max(0, ensemble.Tracks.Sum(value => value.SourceTrack.Events.Count) - arranged);
        var splitDrops = ensemble.SplitReport.Metrics.DroppedNoteCount;
        var duplicates = ensemble.SplitReport.Metrics.DuplicateNoteCount;
        var compatibility = ensemble.Tracks.Select(value => value.Compatibility?.OverallScore ?? 0).ToArray();
        var pitchChanges = ensemble.Tracks.SelectMany(value => value.ArrangementReport?.Changes ?? [])
            .Where(value => value.TargetPitch.HasValue && value.ChangeType is ArrangementChangeType.Transposed or ArrangementChangeType.OctaveShift or ArrangementChangeType.PitchSubstitution)
            .Select(value => Math.Abs(value.TargetPitch!.Value - value.SourcePitch)).ToArray();
        var recommendations = new List<string>();
        if (ensemble.SplitReport.Metrics.BalanceScore < 60m) recommendations.Add("Review the least populated voice; musical continuity currently outweighs balance.");
        if (compatibility.Any(value => value < 70)) recommendations.Add("Try a different scale or transposition on the lowest-compatibility track.");
        if (arrangementDrops + splitDrops > 0) recommendations.Add("Inspect reported note losses before export.");
        return new EnsembleOptimizationReport(source, arranged, arrangementDrops + splitDrops, duplicates,
            source == 0 ? 0m : decimal.Round((arrangementDrops + splitDrops) * 100m / source, 2),
            compatibility.Length == 0 ? 100m : decimal.Round(compatibility.Average(value => (decimal)value), 1),
            compatibility.Length == 0 ? 100 : compatibility.Min(), ensemble.SplitReport.Metrics.VoiceContinuityScore,
            ensemble.SplitReport.Metrics.BalanceScore, pitchChanges.Length == 0 ? 0m : decimal.Round(pitchChanges.Average(value => (decimal)value), 2), recommendations);
    }
}
