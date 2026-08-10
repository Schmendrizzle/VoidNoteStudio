using System.IO.Compression;
using VoidNote.Application.Audio;
using VoidNote.Application.Commands;
using VoidNote.Audio.Import;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Infrastructure.Projects;

namespace VoidNote.Audio.Tests;

public sealed class AudioProjectTests
{
    [Fact]
    public void SelectionAndLoopRegion_HaveValidatedDuration()
    {
        var region = new AudioRegion { Name = "Verse", Start = new(1.25m), End = new(3.75m), LoopEnabled = true }; region.Validate();
        Assert.Equal(2.5m, region.Duration.Seconds); Assert.True(region.LoopEnabled); Assert.Throws<InvalidOperationException>(() => new AudioRegion { Start = new(2), End = new(1) }.Validate());
    }

    [Fact]
    public void AudioTrack_StoresGainMuteSoloAndTimelineOffset()
    {
        var track = new AudioTrack { Name = "Audio", Gain = 0.5m, IsMuted = true, IsSolo = true, Clips = [new() { Name = "Clip", SourceId = Guid.NewGuid(), Start = new(1920), Duration = new(2) }] };
        Assert.Equal(0.5m, track.Gain); Assert.True(track.IsMuted); Assert.True(track.IsSolo); Assert.Equal(1920, track.Clips[0].Start.Ticks);
    }

    [Fact]
    public void TrackOperations_AreUndoable()
    {
        var (project, _, track) = Model(ProjectPathKind.Absolute, "C:/audio.wav"); var history = new UndoRedoService();
        history.Execute(new SetAudioTrackValueCommand<decimal>("Gain", value => track.Gain = value, 1, 0.25m)); Assert.Equal(0.25m, track.Gain); Assert.True(history.Undo()); Assert.Equal(1, track.Gain); Assert.True(history.Redo());
        history.Execute(new RemoveAudioTrackCommand(project, track)); Assert.Empty(project.AudioTracks); Assert.True(history.Undo()); Assert.Single(project.AudioTracks);
    }

    [Theory]
    [InlineData(ProjectPathKind.Relative, "audio/source.wav")]
    [InlineData(ProjectPathKind.Absolute, "C:/music/source.wav")]
    public async Task ExternalAudioReferences_RoundTrip(ProjectPathKind kind, string reference)
    {
        if (!OperatingSystem.IsWindows() && kind == ProjectPathKind.Absolute) reference = "/music/source.wav";
        using var fixtures = new AudioFixtureDirectory(); var path = System.IO.Path.Combine(fixtures.Path, "project.vns"); var (project, source, _) = Model(kind, reference);
        await new VnsProjectStore().SaveAsync(project, path); var loaded = await new VnsProjectStore().LoadAsync(path); var restored = Assert.Single(loaded.AudioSources);
        Assert.Equal(source.Id, restored.Id); Assert.Equal(kind, restored.File!.Kind); Assert.Equal(source.Format.Codec, restored.Format.Codec); Assert.Equal(source.Format.SampleRate, restored.Format.SampleRate);
    }

    [Fact]
    public async Task EmbeddedAudio_IsCopiedAndResolvedOnLoad()
    {
        using var fixtures = new AudioFixtureDirectory(); var audio = fixtures.CreateWave("source.wav"); var projectPath = System.IO.Path.Combine(fixtures.Path, "embedded.vns"); var (project, source, _) = Model(ProjectPathKind.Embedded, "audio/source.wav", audio);
        await new VnsProjectStore().SaveAsync(project, projectPath); using (var archive = ZipFile.OpenRead(projectPath)) Assert.NotNull(archive.GetEntry("audio/source.wav"));
        var loaded = await new VnsProjectStore().LoadAsync(projectPath); var restored = Assert.Single(loaded.AudioSources); Assert.NotNull(restored.ResolvedPath); Assert.True(File.Exists(restored.ResolvedPath)); Assert.Equal(new FileInfo(audio).Length, new FileInfo(restored.ResolvedPath!).Length);
    }

    [Fact]
    public void SourceDiagnostics_DetectMissingAndChangedExternalFiles()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("source.wav"); var file = new FileInfo(path); var (_, source, _) = Model(ProjectPathKind.Absolute, path);
        source = new AudioSource { Id = source.Id, Name = source.Name, File = source.File, SourcePath = path, Format = source.Format, FileSize = file.Length, LastWriteTimeUtc = file.LastWriteTimeUtc };
        Assert.Equal(AudioSourceAvailability.Available, AudioSourceDiagnostics.Inspect(source, null).Availability); File.AppendAllText(path, "changed"); Assert.Equal(AudioSourceAvailability.Changed, AudioSourceDiagnostics.Inspect(source, null).Availability);
        File.Delete(path); Assert.Equal(AudioSourceAvailability.Missing, AudioSourceDiagnostics.Inspect(source, null).Availability);
    }

    [Fact]
    public void ProjectValidation_RejectsClipWithoutSource()
    {
        var project = new VoidNoteProject { AudioTracks = [new() { Name = "Broken", Clips = [new() { Name = "Clip", SourceId = Guid.NewGuid(), Duration = new(1) }] }] };
        Assert.Throws<InvalidOperationException>(project.Validate);
    }

    private static (VoidNoteProject Project, AudioSource Source, AudioTrack Track) Model(ProjectPathKind kind, string reference, string? physicalPath = null)
    {
        var source = new AudioSource { Name = "Source", SourcePath = physicalPath ?? reference, File = new(reference, kind), Format = new() { Container = "WAV", Codec = "PCM", SampleRate = 8000, ChannelCount = 1, BitDepth = 16, Duration = new(1), Channels = [new(0, "Mono")] } };
        var track = new AudioTrack { Name = "Audio", Clips = [new() { Name = "Clip", SourceId = source.Id, Duration = new(1) }] }; var project = new VoidNoteProject { AudioSources = [source], AudioTracks = [track] }; project.Validate(); return (project, source, track);
    }
}
