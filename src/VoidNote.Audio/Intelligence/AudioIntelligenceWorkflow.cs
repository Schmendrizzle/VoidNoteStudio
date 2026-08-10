using System.Diagnostics;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Intelligence;

public sealed record SeparationWorkflowOptions(string Model = "htdemucs", ComputeDevicePreference Device = ComputeDevicePreference.Auto, IReadOnlyList<StemType>? RequestedStems = null);
public sealed record TranscriptionWorkflowOptions(AudioTranscriptionSettings Settings, ComputeDevicePreference Device = ComputeDevicePreference.Auto);
public sealed record TranscriptionWorkflowResult(MidiTrack Track, AudioTranscriptionReport Report);

public interface IAudioIntelligenceWorkflow
{
    Task<StemSet> SeparateAsync(VoidNoteProject project, Guid sourceAudioId, Guid? regionId, SeparationWorkflowOptions options,
        IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<TranscriptionWorkflowResult> TranscribeAsync(VoidNoteProject project, Guid sourceAudioId, Guid? stemId, Guid? regionId,
        TranscriptionWorkflowOptions options, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TranscriptionWorkflowResult>> TranscribeManyAsync(VoidNoteProject project, IReadOnlyList<(Guid SourceAudioId, Guid? StemId)> sources,
        TranscriptionWorkflowOptions options, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Coordinates isolated engines, resource limits, derived assets and normalized project models.</summary>
public sealed class AudioIntelligenceWorkflow(
    IAudioSeparationEngine separationEngine,
    IAudioTranscriptionEngine transcriptionEngine,
    IAudioDecoder decoder,
    IAudioIntelligenceTempManager temp,
    IAiResourceGate resources,
    string derivedAssetDirectory) : IAudioIntelligenceWorkflow
{
    public async Task<StemSet> SeparateAsync(VoidNoteProject project, Guid sourceAudioId, Guid? regionId, SeparationWorkflowOptions options,
        IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(options);
        var source = project.AudioSources.Single(value => value.Id == sourceAudioId);
        var processingSource = ResolveSource(project, source, null, regionId);
        var jobId = Guid.NewGuid(); var jobDirectory = await temp.CreateJobDirectoryAsync(jobId, cancellationToken);
        var createdAssets = new List<string>(); var completed = false;
        var stagedSources = new List<AudioSource>(); var stagedTracks = new List<AudioTrack>(); StemSet? stagedSet = null;
        await using var lease = await resources.AcquireAsync(cancellationToken);
        try
        {
            var request = new SeparationRequest
            {
                JobId = jobId, InputPath = ResolvePath(source), Source = processingSource, OutputDirectory = jobDirectory,
                Model = options.Model, Device = options.Device,
                RequestedStems = options.RequestedStems ?? [StemType.Vocals, StemType.Bass, StemType.Drums, StemType.Other],
            };
            var result = await separationEngine.SeparateAsync(request, progress, cancellationToken);
            progress?.Report(new(AudioIntelligenceStage.ImportingResults, 0.96, "Importing separated stems"));
            var set = new StemSet
            {
                Name = $"{source.Name} stems", Source = processingSource, SeparationEngine = result.Engine,
                EngineVersion = result.EngineVersion, Settings = new() { ["model"] = options.Model, ["device"] = options.Device.ToString() },
                ProcessingMetadata = result.Metadata.ToDictionary(),
            };
            stagedSet = set;
            Directory.CreateDirectory(derivedAssetDirectory);
            foreach (var output in result.Stems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(output.Path)) throw new AudioWorkerException("invalid_result", $"Stem output does not exist: {output.Path}");
                var stemId = Guid.NewGuid(); var extension = Path.GetExtension(output.Path); if (string.IsNullOrWhiteSpace(extension)) extension = ".wav";
                var assetPath = Path.Combine(derivedAssetDirectory, stemId.ToString("N") + extension.ToLowerInvariant());
                File.Copy(output.Path, assetPath, overwrite: false);
                createdAssets.Add(assetPath);
                var format = await decoder.ProbeAsync(assetPath, cancellationToken);
                var audioSourceId = Guid.NewGuid();
                var derived = new AudioSource
                {
                    Id = audioSourceId, Name = output.Name, SourcePath = assetPath, ResolvedPath = assetPath,
                    File = new($"stems/{audioSourceId:N}{extension.ToLowerInvariant()}", ProjectPathKind.Embedded),
                    FileSize = new FileInfo(assetPath).Length, LastWriteTimeUtc = File.GetLastWriteTimeUtc(assetPath), Format = format,
                };
                var stem = new Stem
                {
                    Id = stemId, StemSetId = set.Id, Name = output.Name, SourceAudioId = source.Id, AudioSourceId = derived.Id,
                    Type = output.Type, CustomType = output.CustomType, Engine = result.Engine, EngineVersion = result.EngineVersion,
                    ProcessingSettings = set.Settings.ToDictionary(), Duration = format.Duration, StartOffset = processingSource.StartOffset,
                    File = derived.File, Provenance = new() { SourceAudioId = source.Id, SourceRegionId = regionId, Engine = result.Engine, EngineVersion = result.EngineVersion, CreatedAt = set.CreatedAt },
                };
                set.StemTracks.Add(stem); stagedSources.Add(derived);
                stagedTracks.Add(new AudioTrack
                {
                    Name = output.Name,
                    Clips = [new() { Name = output.Name, SourceId = derived.Id, Start = project.Timeline.ToMusicalTime(processingSource.StartOffset), Duration = format.Duration }],
                });
            }
            project.AudioSources.AddRange(stagedSources); project.AudioTracks.AddRange(stagedTracks); project.StemSets.Add(set); project.Validate();
            progress?.Report(new(AudioIntelligenceStage.Completed, 1, "Stem separation completed"));
            completed = true;
            return set;
        }
        catch (OperationCanceledException) { Rollback(project, stagedSet, stagedSources, stagedTracks); progress?.Report(new(AudioIntelligenceStage.Cancelled, 0, "Stem separation cancelled")); throw; }
        catch { Rollback(project, stagedSet, stagedSources, stagedTracks); progress?.Report(new(AudioIntelligenceStage.Failed, 0, "Stem separation failed")); throw; }
        finally
        {
            if (!completed) foreach (var path in createdAssets) { try { File.Delete(path); } catch (IOException) { } }
            await temp.CleanupJobAsync(jobId);
        }
    }

    public async Task<TranscriptionWorkflowResult> TranscribeAsync(VoidNoteProject project, Guid sourceAudioId, Guid? stemId, Guid? regionId,
        TranscriptionWorkflowOptions options, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(options);
        var source = project.AudioSources.Single(value => value.Id == sourceAudioId);
        Stem? stem = null;
        if (stemId.HasValue) stem = project.StemSets.SelectMany(value => value.StemTracks).Single(value => value.Id == stemId);
        var processingSource = ResolveSource(project, source, stem, regionId);
        var jobId = Guid.NewGuid(); var directory = await temp.CreateJobDirectoryAsync(jobId, cancellationToken);
        await using var lease = await resources.AcquireAsync(cancellationToken);
        var watch = Stopwatch.StartNew();
        try
        {
            var settings = new Dictionary<string, string>();
            if (stem is not null) settings["stemType"] = stem.Type.ToString();
            var engineResult = await transcriptionEngine.TranscribeAsync(new()
            {
                JobId = jobId, InputPath = ResolvePath(source), Source = processingSource, OutputDirectory = directory,
                Mode = options.Settings.Mode, Device = options.Device, Settings = settings,
            }, progress, cancellationToken);
            progress?.Report(new(AudioIntelligenceStage.ImportingResults, 0.96, "Creating editable MIDI notes"));
            var result = AudioTranscriptionProcessor.Create(project.Timeline, processingSource, stemId, source.Name, engineResult, options.Settings, watch.Elapsed);
            lock (project)
            {
                project.MidiTracks.Add(result.Track); project.AudioTranscriptionReports.Add(result.Report); project.Validate();
            }
            progress?.Report(new(AudioIntelligenceStage.Completed, 1, "Audio transcription completed"));
            return result;
        }
        catch (OperationCanceledException) { progress?.Report(new(AudioIntelligenceStage.Cancelled, 0, "Audio transcription cancelled")); throw; }
        catch { progress?.Report(new(AudioIntelligenceStage.Failed, 0, "Audio transcription failed")); throw; }
        finally { await temp.CleanupJobAsync(jobId); }
    }

    public async Task<IReadOnlyList<TranscriptionWorkflowResult>> TranscribeManyAsync(VoidNoteProject project, IReadOnlyList<(Guid SourceAudioId, Guid? StemId)> sources,
        TranscriptionWorkflowOptions options, IProgress<AudioIntelligenceProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var tasks = sources.Select(value => TranscribeAsync(project, value.SourceAudioId, value.StemId, null, options, progress, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private static AudioProcessingSource ResolveSource(VoidNoteProject project, AudioSource source, Stem? stem, Guid? regionId)
    {
        var clip = project.AudioTracks.SelectMany(track => track.Clips).FirstOrDefault(value => value.SourceId == source.Id);
        var clipStart = clip is null ? stem?.StartOffset ?? AbsoluteTime.Zero : project.Timeline.ToAbsoluteTime(clip.Start);
        var duration = clip?.Duration ?? source.Format.Duration;
        var sourceOffset = clip?.TrimIn ?? AbsoluteTime.Zero;
        if (regionId.HasValue)
        {
            var region = project.AudioRegions.Single(value => value.Id == regionId); region.Validate();
            if (region.Start.Seconds < clipStart.Seconds || region.End.Seconds > clipStart.Seconds + duration.Seconds)
                throw new InvalidOperationException("The selected region is outside the audio clip.");
            sourceOffset = new(sourceOffset.Seconds + region.Start.Seconds - clipStart.Seconds);
            return new() { AudioSourceId = source.Id, AudioRegionId = region.Id, StemId = stem?.Id, SourceOffset = sourceOffset, StartOffset = region.Start, Duration = region.Duration };
        }
        return new() { AudioSourceId = source.Id, StemId = stem?.Id, SourceOffset = sourceOffset, StartOffset = clipStart, Duration = duration };
    }

    private static string ResolvePath(AudioSource source) => source.ResolvedPath ?? source.SourcePath;

    private static void Rollback(VoidNoteProject project, StemSet? set, IReadOnlyList<AudioSource> sources, IReadOnlyList<AudioTrack> tracks)
    {
        lock (project)
        {
            if (set is not null) project.StemSets.Remove(set);
            foreach (var track in tracks) project.AudioTracks.Remove(track);
            foreach (var source in sources) project.AudioSources.Remove(source);
        }
    }
}
