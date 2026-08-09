using System.IO.Compression;
using System.Text.Json;
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
            var project = await JsonSerializer.DeserializeAsync<VoidNoteProject>(manifest, SerializerOptions, cancellationToken)
                ?? throw new InvalidDataException("The project manifest contains no project object.");
            project.Validate();
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
                await using var manifest = entry.Open();
                await JsonSerializer.SerializeAsync(manifest, project, SerializerOptions, cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
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
}
