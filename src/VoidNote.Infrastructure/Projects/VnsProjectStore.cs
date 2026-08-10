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
        var entry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("The project container has no project.json manifest.");

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
                    var sourcePath = source.ResolvedPath ?? source.SourcePath;
                    if (!File.Exists(sourcePath)) throw new FileNotFoundException($"Embedded audio source '{source.Name}' is unavailable and the project was not overwritten.", sourcePath);
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
        foreach (var source in project.AudioSources.Where(value => value.File?.Kind == ProjectPathKind.Embedded))
        {
            var entry = archive.GetEntry(source.File!.Path) ?? throw new InvalidDataException($"Embedded audio entry '{source.File.Path}' is missing.");
            if (source.FileSize > 0 && entry.Length != source.FileSize) throw new InvalidDataException($"Embedded audio entry '{source.File.Path}' has an unexpected size.");
            var directory = Path.Combine(Path.GetTempPath(), "VoidNoteStudio", "embedded", project.Id.ToString("N")); Directory.CreateDirectory(directory);
            var extension = Path.GetExtension(source.File.Path); var target = Path.Combine(directory, source.Id.ToString("N") + extension); var temporary = target + ".tmp";
            try
            {
                await using (var input = entry.Open())
                await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await input.CopyToAsync(output, token); await output.FlushAsync(token);
                }
                File.Move(temporary, target, true); source.ResolvedPath = target;
            }
            finally { File.Delete(temporary); }
        }
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
