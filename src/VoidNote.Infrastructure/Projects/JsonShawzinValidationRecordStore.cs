using System.Text.Json;
using VoidNote.Application.Services;
using VoidNote.Application.Shawzin;

namespace VoidNote.Infrastructure.Projects;

public sealed class JsonShawzinValidationRecordStore(IAppPathProvider paths) : IShawzinValidationRecordStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General) { WriteIndented = true };

    public async Task<IReadOnlyList<ShawzinMappingValidationRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.ShawzinValidationRecordsFilePath)) return [];
        await using var input = new FileStream(paths.ShawzinValidationRecordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync<ShawzinMappingValidationRecord[]>(input, Options, cancellationToken) ?? [];
    }

    public async Task SaveAsync(ShawzinMappingValidationRecord record, CancellationToken cancellationToken = default)
    {
        var records = (await LoadAsync(cancellationToken)).Append(record).OrderByDescending(value => value.CreatedAtUtc).Take(100).ToArray();
        var directory = Path.GetDirectoryName(paths.ShawzinValidationRecordsFilePath)!; Directory.CreateDirectory(directory);
        var temporary = paths.ShawzinValidationRecordsFilePath + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(output, records, Options, cancellationToken); await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, paths.ShawzinValidationRecordsFilePath, true);
        }
        finally { File.Delete(temporary); }
    }
}
