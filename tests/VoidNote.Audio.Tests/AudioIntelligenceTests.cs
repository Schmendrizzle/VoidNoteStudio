using Microsoft.Extensions.Logging.Abstractions;
using VoidNote.Audio.Decoding;
using VoidNote.Audio.Intelligence;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Ensemble;
using VoidNote.Shawzin.Mapping;
using VoidNote.Application.Audio;

namespace VoidNote.Audio.Tests;

public sealed class AudioIntelligenceTests
{
    [Fact]
    public async Task SeparationContract_CreatesFourDerivedStemSourcesAndTracks()
    {
        using var fixture = new AudioFixtureDirectory(); var input = fixture.CreateWave("source.wav", seconds: 0.2);
        var project = Project(input, 0.2m); var workflow = Workflow(fixture, new FakeSeparationEngine(fixture), new FakeTranscriptionEngine(StandardNotes()));

        var set = await workflow.SeparateAsync(project, project.AudioSources[0].Id, null, new());

        Assert.Equal([StemType.Vocals, StemType.Bass, StemType.Drums, StemType.Other], set.StemTracks.Select(value => value.Type));
        Assert.Equal(5, project.AudioSources.Count); Assert.Equal(5, project.AudioTracks.Count);
        Assert.All(set.StemTracks, stem => Assert.Equal(project.AudioSources[0].Id, stem.Provenance.SourceAudioId));
        Assert.True(File.Exists(input));
    }

    [Fact]
    public async Task SeparationRegion_PreservesSourceAndMasterOffsets()
    {
        using var fixture = new AudioFixtureDirectory(); var input = fixture.CreateWave("region.wav", seconds: 1);
        var project = Project(input, 1m); project.AudioTracks[0].Clips[0].Start = new MusicalTime(960);
        var region = new AudioRegion { Name = "selection", Start = new(0.75m), End = new(1m) }; project.AudioRegions.Add(region);
        var workflow = Workflow(fixture, new FakeSeparationEngine(fixture), new FakeTranscriptionEngine(StandardNotes()));

        var set = await workflow.SeparateAsync(project, project.AudioSources[0].Id, region.Id, new());

        Assert.Equal(0.25m, set.Source.SourceOffset.Seconds);
        Assert.Equal(0.75m, set.Source.StartOffset.Seconds);
        Assert.All(set.StemTracks, stem => Assert.Equal(0.75m, stem.StartOffset.Seconds));
    }

    [Fact]
    public async Task StemSetRemoval_IsUndoableAndNeverRemovesOriginal()
    {
        using var fixture = new AudioFixtureDirectory(); var input = fixture.CreateWave("undo.wav", seconds: 0.2);
        var project = Project(input, 0.2m); var set = await Workflow(fixture, new FakeSeparationEngine(fixture), new FakeTranscriptionEngine(StandardNotes())).SeparateAsync(project, project.AudioSources[0].Id, null, new());
        var command = new RemoveStemSetCommand(project, set);
        command.Execute(); Assert.Empty(project.StemSets); Assert.Single(project.AudioSources); Assert.True(File.Exists(input));
        command.Undo(); Assert.Single(project.StemSets); Assert.Equal(5, project.AudioSources.Count);
    }

    [Fact]
    public async Task SeparationCancellation_CleansJobDirectoryAndLeavesOriginal()
    {
        using var fixture = new AudioFixtureDirectory(); var input = fixture.CreateWave("cancel.wav"); var project = Project(input, 0.1m);
        var root = Path.Combine(fixture.Path, "jobs"); var workflow = Workflow(fixture, new CancellingSeparationEngine(), new FakeTranscriptionEngine(StandardNotes()), root);
        using var cancellation = new CancellationTokenSource(30);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SeparateAsync(project, project.AudioSources[0].Id, null, new(), cancellationToken: cancellation.Token));

        Assert.Empty(project.StemSets); Assert.True(File.Exists(input));
        Assert.True(!Directory.Exists(root) || !Directory.EnumerateDirectories(root).Any());
    }

    [Fact]
    public async Task Transcription_CreatesEditableMidiWithConfidenceAndProvenance()
    {
        using var fixture = new AudioFixtureDirectory(); var input = fixture.CreateWave("transcribe.wav", seconds: 1); var project = Project(input, 1m);
        var workflow = Workflow(fixture, new FakeSeparationEngine(fixture), new FakeTranscriptionEngine(StandardNotes()));

        var result = await workflow.TranscribeAsync(project, project.AudioSources[0].Id, null, null, new(new()));

        Assert.Equal(3, result.Track.Events.Count); Assert.Equal(3, result.Report.DetectedNotes);
        Assert.All(result.Track.Events, note => { Assert.Equal(MusicalEventSource.AudioTranscription, note.Source); Assert.NotNull(note.AudioProvenance); });
        Assert.Equal(NoteConfidenceLevel.High, result.Track.Events[0].AudioProvenance!.ConfidenceLevel);
    }

    [Theory]
    [InlineData(0.85, NoteConfidenceLevel.High)]
    [InlineData(0.60, NoteConfidenceLevel.Medium)]
    [InlineData(0.59, NoteConfidenceLevel.Low)]
    public void Confidence_UsesConfigurableInclusiveThresholds(double value, NoteConfidenceLevel expected)
        => Assert.Equal(expected, AudioTranscriptionProcessor.Confidence((decimal)value, new()));

    [Fact]
    public void ThresholdFiltering_ReportsEveryDiscardedNote()
    {
        var result = AudioTranscriptionProcessor.Create(ProjectTimeline.CreateDefault(), Source(1m), null, "test",
            new("fake", "1", StandardNotes(), new Dictionary<string, string>()),
            new() { ConfidenceFilter = ConfidenceFilterMode.MinimumThreshold, MinimumConfidence = 0.8m }, TimeSpan.Zero);

        Assert.Single(result.Track.Events); Assert.Equal(2, result.Report.DiscardedNotes);
        Assert.Equal(2, result.Report.Changes.Count(value => value.Type == TranscriptionChangeType.ConfidenceFiltered));
    }

    [Fact]
    public void Quantization_PreservesRawDetectedTimingInProvenance()
    {
        var result = AudioTranscriptionProcessor.Create(ProjectTimeline.CreateDefault(), Source(1m), null, "test",
            new("fake", "1", [new(60, new(0.13m), new(0.21m), 0.8m, 0.9m)], new Dictionary<string, string>()),
            new() { Quantization = TranscriptionQuantization.Sixteenth }, TimeSpan.Zero);

        var note = Assert.Single(result.Track.Events); Assert.NotEqual(note.StartTime, note.AudioProvenance!.OriginalStart);
        Assert.Contains(result.Report.Changes, value => value.Type == TranscriptionChangeType.Quantized);
    }

    [Fact]
    public void Cleanup_IsConservativeAndAuditable()
    {
        var notes = new[]
        {
            new DetectedAudioNote(60, new(0.1m), new(0.01m), 0.7m, 0.9m),
            new DetectedAudioNote(62, new(0.2m), new(0.2m), 0.7m, 0.9m),
            new DetectedAudioNote(62, new(0.2m), new(0.2m), 0.5m, 0.7m),
            new DetectedAudioNote(62, new(0.41m), new(0.2m), 0.8m, 0.95m),
        };
        var result = AudioTranscriptionProcessor.Create(ProjectTimeline.CreateDefault(), Source(1m), null, "test",
            new("fake", "1", notes, new Dictionary<string, string>()),
            new() { RemoveGhostNotes = true, MergeAdjacentNotes = true, DetectDuplicates = true, MergeGap = new(0.02m) }, TimeSpan.Zero);

        Assert.Single(result.Track.Events);
        Assert.Contains(result.Report.Changes, value => value.Type == TranscriptionChangeType.GhostNoteRemoved);
        Assert.Contains(result.Report.Changes, value => value.Type == TranscriptionChangeType.DuplicateRemoved);
        Assert.Contains(result.Report.Changes, value => value.Type == TranscriptionChangeType.NotesMerged);
    }

    [Fact]
    public async Task DrumStem_IsRejectedByBasicPitchAdapter()
    {
        var adapter = new BasicPitchTranscriptionEngine(new NeverCalledWorker());
        await Assert.ThrowsAsync<AudioWorkerException>(() => adapter.TranscribeAsync(new()
        {
            JobId = Guid.NewGuid(), InputPath = "drums.wav", OutputDirectory = "output",
            Source = Source(1m) with { StemId = Guid.NewGuid() }, Settings = new() { ["stemType"] = "Drums" },
        }));
    }

    [Fact]
    public async Task BasicPitch_RejectsUnsupportedExplicitGpuWithoutStartingWorker()
    {
        var adapter = new BasicPitchTranscriptionEngine(new NeverCalledWorker());
        var error = await Assert.ThrowsAsync<AudioWorkerException>(() => adapter.TranscribeAsync(new()
        { JobId = Guid.NewGuid(), InputPath = "input.wav", OutputDirectory = "output", Source = Source(1m), Device = ComputeDevicePreference.Gpu }));
        Assert.Equal("unsupported_device", error.Code);
    }

    [Fact]
    public async Task WorkerCrash_IsContainedAsStructuredEngineFailure()
    {
        var adapter = new DemucsSeparationEngine(new ThrowingWorker("worker_crash"));
        var exception = await Assert.ThrowsAsync<AudioWorkerException>(() => adapter.SeparateAsync(new()
        { JobId = Guid.NewGuid(), InputPath = "input.wav", OutputDirectory = "out", Source = Source(1m) }));
        Assert.Equal("worker_crash", exception.Code);
    }

    [Fact]
    public async Task InvalidWorkerResult_IsRejected()
    {
        var adapter = new BasicPitchTranscriptionEngine(new InvalidWorker());
        await Assert.ThrowsAnyAsync<Exception>(() => adapter.TranscribeAsync(new()
        { JobId = Guid.NewGuid(), InputPath = "input.wav", OutputDirectory = "out", Source = Source(1m) }));
    }

    [Fact]
    public async Task Discovery_ReportsVersionGpuAndCompatibility()
    {
        var installed = await new DemucsSeparationEngine(new DiscoveryWorker("4.1.0", true)).DiscoverAsync();
        var incompatible = await new DemucsSeparationEngine(new DiscoveryWorker("5.0.0", false)).DiscoverAsync();
        Assert.Equal(EngineInstallationState.Installed, installed.State); Assert.True(installed.Capabilities.IsGpuAvailable);
        Assert.Equal(EngineInstallationState.IncompatibleVersion, incompatible.State);
    }

    [Fact]
    public async Task Discovery_MissingWorkerDoesNotThrow()
    {
        var result = await new BasicPitchTranscriptionEngine(new ThrowingWorker("worker_missing")).DiscoverAsync();
        Assert.Equal(EngineInstallationState.Missing, result.State);
    }

    [Fact]
    public void MonophonicMode_ReducesOverlapAndReportsDecision()
    {
        var notes = new[] { new DetectedAudioNote(60, new(0.1m), new(0.5m), 0.7m, 0.7m), new DetectedAudioNote(64, new(0.2m), new(0.2m), 0.8m, 0.9m) };
        var result = AudioTranscriptionProcessor.Create(ProjectTimeline.CreateDefault(), Source(1m), null, "mono",
            new("fake", "1", notes, new Dictionary<string, string>()), new() { Mode = TranscriptionMode.Monophonic }, TimeSpan.Zero);
        Assert.Single(result.Track.Events); Assert.Equal(64, result.Track.Events[0].Pitch);
        Assert.Contains(result.Report.Changes, value => value.Type == TranscriptionChangeType.PolyphonyReduced);
    }

    [Fact]
    public async Task TempManager_RemovesOwnedJobsAndOldOrphansOnly()
    {
        using var fixture = new AudioFixtureDirectory(); var manager = new AudioIntelligenceTempManager(fixture.Path);
        var current = await manager.CreateJobDirectoryAsync(Guid.NewGuid()); var orphan = await manager.CreateJobDirectoryAsync(Guid.NewGuid());
        File.SetLastWriteTimeUtc(Path.Combine(orphan, ".voidnote-ai-job"), DateTime.UtcNow.AddDays(-2));
        var foreign = Path.Combine(fixture.Path, "foreign"); Directory.CreateDirectory(foreign);

        Assert.Equal(1, await manager.CleanupOrphansAsync(TimeSpan.FromDays(1)));
        Assert.True(Directory.Exists(current)); Assert.False(Directory.Exists(orphan)); Assert.True(Directory.Exists(foreign));
    }

    [Fact]
    public async Task ResourceGate_EnforcesConfiguredParallelLimit()
    {
        using var gate = new AiResourceGate(2); var running = 0; var maximum = 0;
        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var lease = await gate.AcquireAsync(); var now = Interlocked.Increment(ref running);
            maximum = Math.Max(maximum, now); await Task.Delay(15); Interlocked.Decrement(ref running);
        });
        await Task.WhenAll(tasks); Assert.Equal(2, maximum);
    }

    [Fact]
    public async Task BatchTranscription_UsesResourceGateAndKeepsTracksSeparate()
    {
        using var fixture = new AudioFixtureDirectory(); var input = fixture.CreateWave("batch.wav", seconds: 1); var project = Project(input, 1m);
        var engine = new CountingTranscriptionEngine(StandardNotes());
        var workflow = Workflow(fixture, new FakeSeparationEngine(fixture), engine, maximumJobs: 1);

        var results = await workflow.TranscribeManyAsync(project,
            [(project.AudioSources[0].Id, null), (project.AudioSources[0].Id, null), (project.AudioSources[0].Id, null)], new(new()));

        Assert.Equal(3, results.Count); Assert.Equal(1, engine.MaximumConcurrency); Assert.Equal(3, project.MidiTracks.Count);
    }

    [Fact]
    public void FakeAudioToMidi_ToShawzinArrangement_Completes()
    {
        var transcription = AudioTranscriptionProcessor.Create(ProjectTimeline.CreateDefault(), Source(1m), null, "bass",
            new("fake", "1", StandardNotes(), new Dictionary<string, string>()), new(), TimeSpan.Zero);
        var mapper = new ShawzinPitchMapper();
        var compatibility = new ShawzinCompatibilityAnalyzer(mapper).Analyze(transcription.Track, ProjectTimeline.CreateDefault(), BuiltInShawzinDefinitions.Default, ShawzinScale.Chromatic);
        var arrangement = new ShawzinArranger(mapper).Arrange(transcription.Track, ProjectTimeline.CreateDefault(),
            BuiltInShawzinDefinitions.Default, new() { Scale = ShawzinScale.Chromatic, Strategies = ArrangementStrategy.Strict });
        var encoded = new WarframeShawzinCodeEncoder().Encode(new ShawzinSong(arrangement.Track!));
        Assert.True(compatibility.OverallScore > 0); Assert.True(arrangement.IsSuccess); Assert.Equal(3, arrangement.Report.OutputNoteCount);
        Assert.True(encoded.IsSuccess); Assert.False(string.IsNullOrWhiteSpace(encoded.Code));
    }

    [Fact]
    public void FakePolyphonicAudioToMidi_ToMultiShawzinSplitter_CompletesWithoutLoss()
    {
        var notes = new[] { new DetectedAudioNote(48, new(0.1m), new(0.4m), 0.9m, 0.9m), new DetectedAudioNote(60, new(0.1m), new(0.4m), 0.9m, 0.9m), new DetectedAudioNote(67, new(0.1m), new(0.4m), 0.9m, 0.9m) };
        var transcription = AudioTranscriptionProcessor.Create(ProjectTimeline.CreateDefault(), Source(1m), null, "poly",
            new("fake", "1", notes, new Dictionary<string, string>()), new() { Mode = TranscriptionMode.Polyphonic }, TimeSpan.Zero);
        var split = new MultiShawzinSplitter(new VoiceSalienceAnalyzer()).Split([transcription.Track], new() { ShawzinCount = 3 });
        var mapper = new ShawzinPitchMapper();
        var ensemble = new ShawzinEnsembleArranger(new ShawzinScaleAnalyzer(), new ShawzinTranspositionAnalyzer(mapper),
            new ShawzinCompatibilityAnalyzer(mapper), new ShawzinArranger(mapper)).Arrange(split, ProjectTimeline.CreateDefault());
        Assert.Equal(3, split.Voices.Count); Assert.Equal(3, split.Report.Metrics.AssignedNoteCount); Assert.Equal(0, split.Report.Metrics.DroppedNoteCount);
        Assert.Equal(3, ensemble.Tracks.Count); Assert.All(ensemble.Tracks, value => Assert.NotNull(value.ShawzinTrack));
    }

    private static AudioIntelligenceWorkflow Workflow(AudioFixtureDirectory fixture, IAudioSeparationEngine separation, IAudioTranscriptionEngine transcription,
        string? tempRoot = null, int maximumJobs = 1) => new(separation, transcription, new WaveAudioDecoder(),
            new AudioIntelligenceTempManager(tempRoot ?? Path.Combine(fixture.Path, "jobs")), new AiResourceGate(maximumJobs), Path.Combine(fixture.Path, "assets"));

    private static VoidNoteProject Project(string path, decimal duration)
    {
        var source = new AudioSource { Name = "source", SourcePath = path, ResolvedPath = path, File = new(path, ProjectPathKind.Absolute), FileSize = new FileInfo(path).Length,
            LastWriteTimeUtc = File.GetLastWriteTimeUtc(path), Format = new() { Container = "WAV", Codec = "pcm_s16le", SampleRate = 8000, ChannelCount = 1, BitDepth = 16, Duration = new(duration) } };
        return new() { AudioSources = [source], AudioTracks = [new() { Name = "source", Clips = [new() { Name = "source", SourceId = source.Id, Duration = new(duration) }] }] };
    }

    private static AudioProcessingSource Source(decimal duration) => new() { AudioSourceId = Guid.NewGuid(), Duration = new(duration) };
    private static IReadOnlyList<DetectedAudioNote> StandardNotes() => [new(60, new(0.1m), new(0.2m), 0.9m, 0.95m), new(64, new(0.4m), new(0.2m), 0.7m, 0.75m), new(67, new(0.7m), new(0.2m), 0.5m, 0.4m)];

    private sealed class FakeSeparationEngine(AudioFixtureDirectory fixture) : IAudioSeparationEngine
    {
        public string Id => "fake-separation";
        public Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(new EngineDiscoveryResult(Id, EngineInstallationState.Installed, "1", null, new(true, true, false, [StemType.Vocals, StemType.Bass, StemType.Drums, StemType.Other], []), "ready"));
        public Task<SeparationResult> SeparateAsync(SeparationRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var outputs = new[] { StemType.Vocals, StemType.Bass, StemType.Drums, StemType.Other }.Select(type => new SeparatedStemFile(type, null, type.ToString(), fixture.CreateWave($"{request.JobId:N}-{type}.wav", seconds: 0.1), new(0.1m))).ToArray();
            return Task.FromResult(new SeparationResult(Id, "1", outputs, new Dictionary<string, string>()));
        }
    }

    private sealed class CancellingSeparationEngine : IAudioSeparationEngine
    {
        public string Id => "cancel";
        public Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<SeparationResult> SeparateAsync(SeparationRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); throw new InvalidOperationException(); }
    }

    private class FakeTranscriptionEngine(IReadOnlyList<DetectedAudioNote> notes) : IAudioTranscriptionEngine
    {
        public string Id => "fake-transcription";
        public Task<EngineDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(new EngineDiscoveryResult(Id, EngineInstallationState.Installed, "1", null, new(true, false, false, [], [TranscriptionMode.Auto, TranscriptionMode.Polyphonic]), "ready"));
        public virtual Task<TranscriptionEngineResult> TranscribeAsync(TranscriptionRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TranscriptionEngineResult(Id, "1", notes, new Dictionary<string, string>()));
    }

    private sealed class CountingTranscriptionEngine(IReadOnlyList<DetectedAudioNote> notes) : FakeTranscriptionEngine(notes)
    {
        private int _running; public int MaximumConcurrency { get; private set; }
        public override async Task<TranscriptionEngineResult> TranscribeAsync(TranscriptionRequest request, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
        { var current = Interlocked.Increment(ref _running); MaximumConcurrency = Math.Max(MaximumConcurrency, current); try { await Task.Delay(20, cancellationToken); return await base.TranscribeAsync(request, progress, cancellationToken); } finally { Interlocked.Decrement(ref _running); } }
    }

    private sealed class NeverCalledWorker : IAudioWorkerClient
    { public Task<WorkerResult> ExecuteAsync(WorkerRequest request, IProgress<WorkerProgressMessage>? progress = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Worker must not be called for drums."); }
    private sealed class ThrowingWorker(string code) : IAudioWorkerClient
    { public Task<WorkerResult> ExecuteAsync(WorkerRequest request, IProgress<WorkerProgressMessage>? progress = null, CancellationToken cancellationToken = default) => throw new AudioWorkerException(code, code); }
    private sealed class InvalidWorker : IAudioWorkerClient
    { public Task<WorkerResult> ExecuteAsync(WorkerRequest request, IProgress<WorkerProgressMessage>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(new WorkerResult(1, request.JobId, true, default, default, [])); }
    private sealed class DiscoveryWorker(string version, bool gpu) : IAudioWorkerClient
    {
        public Task<WorkerResult> ExecuteAsync(WorkerRequest request, IProgress<WorkerProgressMessage>? progress = null, CancellationToken cancellationToken = default)
        {
            var outputs = System.Text.Json.JsonSerializer.SerializeToElement(new { installed = true, version, modelAvailable = true, gpuAvailable = gpu, executablePath = "python", message = "ready" });
            return Task.FromResult(new WorkerResult(1, request.JobId, true, outputs, System.Text.Json.JsonSerializer.SerializeToElement(new { }), []));
        }
    }
}
