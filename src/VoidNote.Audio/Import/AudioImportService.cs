using Microsoft.Extensions.Logging;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Import;

public sealed record AudioImportProgress(double Fraction, string Stage);
public sealed record AudioImportOptions(ProjectPathKind StorageKind, string? ProjectDirectory = null, MusicalTime? Start = null);
public sealed record AudioImportResult(AudioSource Source, AudioTrack Track);

public interface IAudioImportService
{
    Task<AudioImportResult> ImportAsync(VoidNoteProject project, string path, AudioImportOptions options,
        IProgress<AudioImportProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class AudioImportException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Validates, probes and adds immutable audio plus one timeline clip without overwriting existing entries.</summary>
public sealed class AudioImportService(IAudioDecoder decoder, ILogger<AudioImportService> logger) : IAudioImportService
{
    public async Task<AudioImportResult> ImportAsync(VoidNoteProject project, string path, AudioImportOptions options,
        IProgress<AudioImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested(); progress?.Report(new(0.05, "Validating file"));
        var fullPath = Path.GetFullPath(path); var extension = Path.GetExtension(fullPath);
        if (!SupportedAudioFormats.Extensions.Contains(extension)) throw new AudioImportException($"Unsupported audio format '{extension}'. Supported formats are WAV, FLAC and MP3.");
        if (!File.Exists(fullPath)) throw new AudioImportException($"Audio file not found: {fullPath}");
        FileInfo file;
        try { file = new FileInfo(fullPath); using var _ = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw new AudioImportException("The audio file is not readable.", exception); }
        if (project.AudioSources.Any(source => SamePath(source.SourcePath, fullPath))) throw new AudioImportException("This source file is already part of the project. The existing source was left unchanged.");
        progress?.Report(new(0.15, "Reading audio metadata"));
        AudioFormatInfo format;
        try { format = await decoder.ProbeAsync(fullPath, cancellationToken); }
        catch (AudioDecoderException exception) { logger.LogWarning(exception, "Audio probe failed for {Path}", fullPath); throw new AudioImportException(exception.Message, exception); }
        cancellationToken.ThrowIfCancellationRequested(); progress?.Report(new(0.75, "Creating project track"));
        var id = Guid.NewGuid(); var reference = CreateReference(id, extension, fullPath, options);
        var source = new AudioSource
        {
            Id = id, Name = Path.GetFileNameWithoutExtension(fullPath), SourcePath = fullPath, File = reference, Format = format,
            FileSize = file.Length, LastWriteTimeUtc = file.LastWriteTimeUtc, ResolvedPath = fullPath,
        };
        var track = new AudioTrack
        {
            Name = source.Name,
            Clips = [new AudioClip { Name = source.Name, SourceId = source.Id, Start = options.Start ?? MusicalTime.Zero, Duration = format.Duration }],
        };
        project.AudioSources.Add(source); project.AudioTracks.Add(track); project.Validate();
        logger.LogInformation("Imported {Codec} audio {Path} as source {SourceId}", format.Codec, fullPath, source.Id);
        progress?.Report(new(1, "Audio imported")); return new(source, track);
    }

    private static ProjectFileReference CreateReference(Guid id, string extension, string source, AudioImportOptions options) => options.StorageKind switch
    {
        ProjectPathKind.Embedded => new($"audio/{id:N}{extension.ToLowerInvariant()}", ProjectPathKind.Embedded),
        ProjectPathKind.Absolute => new(source, ProjectPathKind.Absolute),
        ProjectPathKind.Relative when !string.IsNullOrWhiteSpace(options.ProjectDirectory) => new(Path.GetRelativePath(Path.GetFullPath(options.ProjectDirectory), source), ProjectPathKind.Relative),
        ProjectPathKind.Relative => throw new AudioImportException("A project directory is required for a relative external reference."),
        _ => throw new AudioImportException("Unsupported audio storage mode."),
    };

    private static bool SamePath(string left, string right) => !string.IsNullOrWhiteSpace(left) && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
