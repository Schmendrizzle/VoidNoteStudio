using Microsoft.Extensions.Logging.Abstractions;
using VoidNote.Audio.Decoding;
using VoidNote.Audio.Import;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Tests;

public sealed class AudioImportTests
{
    [Fact]
    public async Task WavImport_ReadsRealMetadataAndCreatesTrackAtZero()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("mono.wav", 8000, 1, 0.25);
        var project = new VoidNoteProject(); var result = await Service(new WaveAudioDecoder()).ImportAsync(project, path, new(ProjectPathKind.Absolute));
        Assert.Equal("WAV", result.Source.Format.Container); Assert.Equal(8000, result.Source.Format.SampleRate); Assert.Equal(1, result.Source.Format.ChannelCount);
        Assert.Equal(16, result.Source.Format.BitDepth); Assert.Equal(0.25m, result.Source.Format.Duration.Seconds); Assert.Equal(MusicalTime.Zero, Assert.Single(result.Track.Clips).Start);
    }

    [Theory]
    [InlineData("synthetic-sine.flac", "flac", 16)]
    [InlineData("synthetic-sine.mp3", "mp3", null)]
    public async Task CompressedImport_UsesDecoderContract(string name, string codec, int? bitDepth)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        var result = await Service(new SyntheticCompressedDecoder()).ImportAsync(new(), path, new(ProjectPathKind.Absolute));
        Assert.Equal(codec, result.Source.Format.Codec); Assert.Equal(bitDepth, result.Source.Format.BitDepth); Assert.Equal(2, result.Source.Format.ChannelCount);
        var signature = await File.ReadAllBytesAsync(path); Assert.True(name.EndsWith(".flac", StringComparison.Ordinal) ? signature.AsSpan(0, 4).SequenceEqual("fLaC"u8) : signature.AsSpan(0, 3).SequenceEqual("ID3"u8) || signature[0] == 0xff);
    }

    [Fact]
    public async Task StereoWavImport_PreservesChannelAndRateMetadata()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("stereo.wav", 48000, 2, 0.02);
        var source = (await Service(new WaveAudioDecoder()).ImportAsync(new(), path, new(ProjectPathKind.Absolute))).Source;
        Assert.Equal(48000, source.Format.SampleRate); Assert.Equal(["Left", "Right"], source.Format.Channels.Select(value => value.Name));
    }

    [Fact]
    public async Task MissingFile_IsRejectedWithoutProjectMutation()
    {
        var project = new VoidNoteProject(); await Assert.ThrowsAsync<AudioImportException>(() => Service(new WaveAudioDecoder()).ImportAsync(project, "missing.wav", new(ProjectPathKind.Absolute)));
        Assert.Empty(project.AudioSources); Assert.Empty(project.AudioTracks);
    }

    [Fact]
    public async Task UnsupportedExtension_IsRejected()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateCompressedContractFixture("audio.aac");
        await Assert.ThrowsAsync<AudioImportException>(() => Service(new SyntheticCompressedDecoder()).ImportAsync(new(), path, new(ProjectPathKind.Absolute)));
    }

    [Fact]
    public async Task InvalidWav_IsRejectedCleanly()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateCompressedContractFixture("invalid.wav");
        var exception = await Assert.ThrowsAsync<AudioImportException>(() => Service(new WaveAudioDecoder()).ImportAsync(new(), path, new(ProjectPathKind.Absolute)));
        Assert.IsType<AudioDecoderException>(exception.InnerException);
    }

    [Fact]
    public async Task DuplicateImport_DoesNotOverwriteOrAddItems()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("same.wav"); var project = new VoidNoteProject(); var service = Service(new WaveAudioDecoder());
        await service.ImportAsync(project, path, new(ProjectPathKind.Absolute)); await Assert.ThrowsAsync<AudioImportException>(() => service.ImportAsync(project, path, new(ProjectPathKind.Absolute)));
        Assert.Single(project.AudioSources); Assert.Single(project.AudioTracks);
    }

    [Fact]
    public async Task RelativeAndEmbeddedReferences_AreExplicit()
    {
        using var fixtures = new AudioFixtureDirectory(); var first = fixtures.CreateWave("relative.wav"); var second = fixtures.CreateWave("embedded.wav");
        var relative = await Service(new WaveAudioDecoder()).ImportAsync(new(), first, new(ProjectPathKind.Relative, fixtures.Path));
        var embedded = await Service(new WaveAudioDecoder()).ImportAsync(new(), second, new(ProjectPathKind.Embedded));
        Assert.Equal(ProjectPathKind.Relative, relative.Source.File!.Kind); Assert.False(System.IO.Path.IsPathRooted(relative.Source.File.Path));
        Assert.Equal(ProjectPathKind.Embedded, embedded.Source.File!.Kind); Assert.StartsWith("audio/", embedded.Source.File.Path);
    }

    [Fact]
    public async Task Import_ReportsProgressAndSupportsCancellation()
    {
        using var fixtures = new AudioFixtureDirectory(); var path = fixtures.CreateWave("cancel.wav"); var values = new List<AudioImportProgress>(); var progress = new InlineProgress<AudioImportProgress>(values.Add);
        await Service(new WaveAudioDecoder()).ImportAsync(new(), path, new(ProjectPathKind.Absolute), progress); Assert.Equal(1, values[^1].Fraction);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(new WaveAudioDecoder()).ImportAsync(new(), path, new(ProjectPathKind.Absolute), cancellationToken: cancellation.Token));
    }

    private static AudioImportService Service(IAudioDecoder decoder) => new(decoder, NullLogger<AudioImportService>.Instance);
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
