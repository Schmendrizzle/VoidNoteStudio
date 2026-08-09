using System.Text.Json;
using VoidNote.Application.Services;
using VoidNote.Application.Settings;

namespace VoidNote.Infrastructure.Settings;

/// <summary>Persists versioned settings as local JSON using atomic replacement.</summary>
public sealed class JsonSettingsStore(IAppPathProvider pathProvider) : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    private readonly IAppPathProvider _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

    /// <inheritdoc />
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_pathProvider.SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = new FileStream(
                _pathProvider.SettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                ?? throw new InvalidDataException("The settings file contains no settings object.");

            if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
            {
                throw new InvalidDataException($"Unsupported settings schema version: {settings.SchemaVersion}.");
            }

            return settings;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The settings file is not valid JSON.", exception);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported settings schema version: {settings.SchemaVersion}.");
        }

        var directory = Path.GetDirectoryName(_pathProvider.SettingsFilePath)
            ?? throw new InvalidOperationException("The settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _pathProvider.SettingsFilePath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _pathProvider.SettingsFilePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
