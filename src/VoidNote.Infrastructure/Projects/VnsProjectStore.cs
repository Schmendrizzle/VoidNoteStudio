using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using VoidNote.Application.Projects;
using VoidNote.Domain.Projects;

namespace VoidNote.Infrastructure.Projects;

/// <summary>Reads and writes versioned ZIP-based <c>.vns</c> project containers.</summary>
public sealed class VnsProjectStore : IProjectStore
{
    private const string ManifestEntryName = "project.json";
    private const int MaximumEntryCount = 4096;
    private const long MaximumManifestBytes = 32L * 1024 * 1024;
    private const long MaximumEmbeddedAssetBytes = 4L * 1024 * 1024 * 1024;
    private const long MaximumTotalExpandedBytes = 8L * 1024 * 1024 * 1024;
    private const int MaximumCompressionRatio = 250;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    /// <inheritdoc />
    public async Task<VoidNoteProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidatePath(path);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        ValidateArchive(archive);
        var entry = archive.Entries.SingleOrDefault(value => string.Equals(value.FullName, ManifestEntryName, StringComparison.Ordinal))
            ?? throw new InvalidDataException("The project container has no project.json manifest.");
        if (entry.Length > MaximumManifestBytes) throw new InvalidDataException("The project manifest exceeds the safe size limit.");

        try
        {
            await using var manifest = entry.Open();
            var document = await JsonNode.ParseAsync(manifest, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("The project manifest contains no project object.");
            var loadedVersion = document["FormatVersion"]?.GetValue<int>()
                ?? document["formatVersion"]?.GetValue<int>() ?? 1;
            if (loadedVersion is < 1 or > VoidNoteProject.CurrentFormatVersion)
                throw new InvalidDataException($"Unsupported project format version: {loadedVersion}.");
            MigrateManifest(document, loadedVersion);
            var project = document.Deserialize<VoidNoteProject>(SerializerOptions)
                ?? throw new InvalidDataException("The project manifest contains no project object.");
            project.LoadedFormatVersion = loadedVersion;
            project.Validate();
            await ExtractEmbeddedAudioAsync(project, archive, cancellationToken);
            return project;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The project manifest is not valid JSON.", exception);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(VoidNoteProject project, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidatePath(path);
        project.Validate();

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The project path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                var entry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                await using (var manifest = entry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifest, project, SerializerOptions, cancellationToken);
                    await manifest.FlushAsync(cancellationToken);
                }
                foreach (var source in project.AudioSources.Where(value => value.File?.Kind == ProjectPathKind.Embedded))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateArchiveEntryPath(source.File!.Path);
                    var sourcePath = source.ResolvedPath ?? source.SourcePath;
                    if (!File.Exists(sourcePath)) throw new FileNotFoundException($"Embedded audio source '{source.Name}' is unavailable and the project was not overwritten.", sourcePath);
                    if (new FileInfo(sourcePath).Length > MaximumEmbeddedAssetBytes) throw new InvalidDataException($"Embedded audio source '{source.Name}' exceeds the safe size limit.");
                    var audioEntry = archive.CreateEntry(source.File!.Path, CompressionLevel.NoCompression);
                    await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await using var output = audioEntry.Open(); await input.CopyToAsync(output, cancellationToken);
                }
            }

            if (project.LoadedFormatVersion < VoidNoteProject.CurrentFormatVersion && File.Exists(fullPath))
            {
                var backupPath = fullPath + $".v{project.LoadedFormatVersion}.bak";
                if (!File.Exists(backupPath)) File.Copy(fullPath, backupPath);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
            project.LoadedFormatVersion = VoidNoteProject.CurrentFormatVersion;
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(Path.GetExtension(path), ".vns", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("VoidNote project files must use the .vns extension.", nameof(path));
        }
    }

    private static async Task ExtractEmbeddedAudioAsync(VoidNoteProject project, ZipArchive archive, CancellationToken token)
    {
        long extractedBytes = 0;
        foreach (var source in project.AudioSources.Where(value => value.File?.Kind == ProjectPathKind.Embedded))
        {
            ValidateArchiveEntryPath(source.File!.Path);
            var entry = archive.Entries.SingleOrDefault(value => string.Equals(value.FullName, source.File.Path, StringComparison.Ordinal))
                ?? throw new InvalidDataException($"Embedded audio entry '{source.File.Path}' is missing.");
            ValidateAssetEntry(entry);
            if (source.FileSize > 0 && entry.Length != source.FileSize) throw new InvalidDataException($"Embedded audio entry '{source.File.Path}' has an unexpected size.");
            extractedBytes = checked(extractedBytes + entry.Length);
            if (extractedBytes > MaximumTotalExpandedBytes) throw new InvalidDataException("The project exceeds the total expanded-size limit.");
            var directory = Path.Combine(Path.GetTempPath(), "VoidNoteStudio", "embedded", project.Id.ToString("N")); Directory.CreateDirectory(directory);
            var extension = Path.GetExtension(source.File.Path); var target = Path.Combine(directory, source.Id.ToString("N") + extension); var temporary = target + ".tmp";
            try
            {
                await using (var input = entry.Open())
                await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await CopyWithLimitAsync(input, output, entry.Length, token); await output.FlushAsync(token);
                }
                File.Move(temporary, target, true); source.ResolvedPath = target;
            }
            finally { File.Delete(temporary); }
        }
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumEntryCount) throw new InvalidDataException("The project container has too many entries.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            ValidateArchiveEntryPath(entry.FullName);
            if (!names.Add(entry.FullName)) throw new InvalidDataException($"The project container contains duplicate entry '{entry.FullName}'.");
            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > MaximumTotalExpandedBytes) throw new InvalidDataException("The project exceeds the total expanded-size limit.");
            if (IsSymbolicLink(entry)) throw new InvalidDataException($"Symbolic-link archive entry '{entry.FullName}' is not allowed.");
        }
    }

    private static void ValidateAssetEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumEmbeddedAssetBytes) throw new InvalidDataException($"Archive entry '{entry.FullName}' exceeds the safe size limit.");
        if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > MaximumCompressionRatio)
            throw new InvalidDataException($"Archive entry '{entry.FullName}' has a suspicious compression ratio.");
    }

    private static void ValidateArchiveEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || Path.IsPathRooted(path) || path.StartsWith('/') || path.StartsWith('\\'))
            throw new InvalidDataException("Project archive paths must be non-empty relative paths.");
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
            throw new InvalidDataException($"Unsafe project archive path '{path}'.");
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixMode == 0xA000;
    }

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long expectedLength, CancellationToken token)
    {
        var buffer = new byte[81920]; long copied = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) break;
            copied = checked(copied + read);
            if (copied > expectedLength || copied > MaximumEmbeddedAssetBytes) throw new InvalidDataException("An embedded project asset exceeded its declared safe size.");
            await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
        }
        if (copied != expectedLength) throw new InvalidDataException("An embedded project asset ended before its declared size.");
    }

    private static void MigrateManifest(JsonNode document, int loadedVersion)
    {
        if (document is not JsonObject root) throw new InvalidDataException("The project manifest root must be an object.");
        if (loadedVersion == 1)
        {
            var legacyStems = root["Stems"]?.DeepClone() ?? root["stems"]?.DeepClone() ?? new JsonArray();
            root["LegacyStemReferences"] = legacyStems;
            root.Remove("Stems");
            root.Remove("stems");
            root["StemSets"] ??= new JsonArray();
            root["AudioTranscriptionReports"] ??= new JsonArray();
        }
        if (loadedVersion <= 2) root["CreatorSessions"] ??= new JsonArray();
        if (loadedVersion <= 3)
        {
            root["MandachordArrangements"] ??= new JsonArray();
            root["MandachordSoundSets"] ??= new JsonArray();
        }
        root["FormatVersion"] = VoidNoteProject.CurrentFormatVersion;
        root.Remove("formatVersion");
    }
}
