using VoidNote.Domain.Projects;

namespace VoidNote.Audio.Import;

public enum AudioSourceAvailability { Available, Missing, Changed, EmbeddedInProject, InvalidReference }
public sealed record AudioSourceDiagnostic(AudioSourceAvailability Availability, string Message, string? ResolvedPath);

public static class AudioSourceDiagnostics
{
    public static AudioSourceDiagnostic Inspect(AudioSource source, string? projectPath)
    {
        if (source.File is null) return new(AudioSourceAvailability.InvalidReference, "The audio source has no file reference.", null);
        if (source.File.Kind == ProjectPathKind.Embedded) return new(AudioSourceAvailability.EmbeddedInProject, "The source is embedded in the project container.", null);
        var path = source.File.Kind == ProjectPathKind.Absolute ? source.File.Path : projectPath is null ? null : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, source.File.Path));
        if (path is null) return new(AudioSourceAvailability.InvalidReference, "A relative source cannot be resolved before the project has a location.", null);
        if (!File.Exists(path)) return new(AudioSourceAvailability.Missing, $"Source audio missing: {path}", path);
        var file = new FileInfo(path);
        if (source.FileSize > 0 && (file.Length != source.FileSize || file.LastWriteTimeUtc != source.LastWriteTimeUtc.UtcDateTime))
            return new(AudioSourceAvailability.Changed, "The external audio source changed since import.", path);
        return new(AudioSourceAvailability.Available, "Audio source is available.", path);
    }
}
