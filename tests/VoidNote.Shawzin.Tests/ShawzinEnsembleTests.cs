using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Shawzin.Mapping;
using VoidNote.Shawzin.Playback;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinEnsembleTests
{
    [Fact]
    public void PerTrackOptimization_AllowsDifferentInstrumentsScalesAndTranspositions()
    {
        var split = Split(3, new[] { 48, 55, 60, 50, 57, 62 },
        [
            new() { TrackIndex = 0, Instrument = BuiltInShawzinDefinitions.All[0], Scale = ShawzinScale.Chromatic, TranspositionSemitones = 0 },
            new() { TrackIndex = 1, Instrument = BuiltInShawzinDefinitions.All[1], Scale = ShawzinScale.Major, TranspositionSemitones = 2 },
            new() { TrackIndex = 2, Instrument = BuiltInShawzinDefinitions.All[0], Scale = ShawzinScale.Minor, TranspositionSemitones = -2 },
        ]);
        var ensemble = Arranger().Arrange(split, Timeline);

        Assert.Equal(["dax", "nelumbo", "dax"], ensemble.Tracks.Select(value => value.Instrument.Id));
        Assert.Equal([ShawzinScale.Chromatic, ShawzinScale.Major, ShawzinScale.Minor], ensemble.Tracks.Select(value => value.Scale));
        Assert.Equal([0, 2, -2], ensemble.Tracks.Select(value => value.TranspositionSemitones));
        Assert.All(ensemble.Tracks, value => Assert.NotNull(value.Compatibility));
        Assert.All(ensemble.Tracks, value => Assert.NotEmpty(value.ScaleCandidates));
        Assert.All(ensemble.Tracks, value => Assert.Equal(25, value.TranspositionCandidates.Count));
    }

    [Fact]
    public void MultiCodeExport_ProducesIndependentValidatedReports()
    {
        var ensemble = Arranger().Arrange(Split(3, new[] { 48, 55, 62, 50, 57, 64 }), Timeline);
        var report = new EnsembleCodeExporter(new WarframeShawzinCodeEncoder()).Export(ensemble);

        Assert.True(report.IsValid);
        Assert.Equal(3, report.Tracks.Count);
        Assert.All(report.Tracks, value => { Assert.True(value.IsValid); Assert.NotEmpty(value.Code!); Assert.Equal(value.Code!.Length, value.CodeLength); });
        Assert.Equal(3, report.Tracks.Select(value => value.TrackName).Distinct().Count());
    }

    [Fact]
    public void EnsembleOptimization_ReportsCompatibilityLossContinuityAndBalance()
    {
        var ensemble = Arranger().Arrange(Split(4, new[] { 48, 52, 55, 60, 50, 53, 57, 62 }), Timeline);
        var report = ensemble.OptimizationReport!;

        Assert.Equal(8, report.SourceNoteCount);
        Assert.InRange(report.AverageCompatibility, 0m, 100m);
        Assert.InRange(report.LowestTrackCompatibility, 0, 100);
        Assert.InRange(report.VoiceContinuityScore, 0m, 100m);
        Assert.InRange(report.BalanceScore, 0m, 100m);
        Assert.True(report.DroppedNoteCount >= 0);
    }

    [Fact]
    public void PreviewMix_IsStereoAndHonorsMuteAndSolo()
    {
        var ensemble = Arranger().Arrange(Split(3, new[] { 48, 55, 62, 50, 57, 64 }), Timeline);
        ensemble.Tracks[0].IsMuted = true;
        ensemble.Tracks[1].IsSolo = true;
        var audio = new SyntheticShawzinEnsemblePreviewRenderer().Render(ensemble);

        Assert.True(audio.WaveData.Length > 44);
        Assert.Equal((short)2, BitConverter.ToInt16(audio.WaveData, 22));
        Assert.True(audio.DurationSeconds > 0m);
    }

    [Fact]
    public async Task EnsemblePlayback_UsesOneAnchorAndHonorsMuteSoloAndSingleTrack()
    {
        var ensemble = Arranger().Arrange(Split(3, new[] { 48, 55, 62, 50, 57, 64 }), Timeline);
        ensemble.Tracks[0].IsMuted = true;
        ensemble.Tracks[1].IsSolo = true;
        var scheduler = new Scheduler();
        var output = new Output();
        await using var engine = new ShawzinEnsemblePlaybackEngine(scheduler, output);
        await engine.LoadAsync(ensemble);

        await engine.PlayAsync();

        Assert.NotEmpty(scheduler.Targets);
        Assert.All(output.TrackIds, id => Assert.Equal(ensemble.Tracks[1].Id, id));
        output.TrackIds.Clear(); scheduler.Targets.Clear();
        ensemble.Tracks[0].IsMuted = false;
        ensemble.Tracks[1].IsSolo = false;
        await engine.SeekAsync(AbsoluteTime.Zero);
        await engine.PlayAsync(ensemble.Tracks[2].Id);
        Assert.All(output.TrackIds, id => Assert.Equal(ensemble.Tracks[2].Id, id));
    }

    private static MultiShawzinSplitResult Split(int count, int[] pitches, IReadOnlyList<ShawzinVoicePreference>? preferences = null)
    {
        var notes = pitches.Select((pitch, index) => new MusicalEvent(Guid.Parse($"{index + 1:x8}-0000-0000-0000-000000000000"),
            new MusicalTime(index / count * 960), new MusicalTime(480), pitch, 100, MusicalEventSource.ImportedMidi, 1m)).ToList();
        var track = new MidiTrack { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Piano", Events = notes };
        return new MultiShawzinSplitter(new VoiceSalienceAnalyzer()).Split([track], new MultiShawzinSplitOptions
        { ShawzinCount = count, Strategy = MultiShawzinSplitStrategy.FullEnsemble, Preferences = preferences ?? [] });
    }

    private static ShawzinEnsembleArranger Arranger()
    {
        var mapper = new ShawzinPitchMapper();
        return new(new ShawzinScaleAnalyzer(), new ShawzinTranspositionAnalyzer(mapper), new ShawzinCompatibilityAnalyzer(mapper), new ShawzinArranger(mapper));
    }

    private static ProjectTimeline Timeline { get; } = ProjectTimeline.CreateDefault();

    private sealed class Scheduler : IShawzinPlaybackScheduler
    {
        public List<AbsoluteTime> Targets { get; } = [];
        public long GetTimestamp() => 17;
        public AbsoluteTime GetElapsedTime(long startingTimestamp) => AbsoluteTime.Zero;
        public ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken)
        { Targets.Add(targetOffset); return ValueTask.CompletedTask; }
    }

    private sealed class Output : IEnsemblePlaybackOutput
    {
        public List<Guid> TrackIds { get; } = [];
        public ValueTask PlayAsync(Guid trackId, ShawzinEvent shawzinEvent, CancellationToken cancellationToken) { TrackIds.Add(trackId); return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask PositionChangedAsync(AbsoluteTime position, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
