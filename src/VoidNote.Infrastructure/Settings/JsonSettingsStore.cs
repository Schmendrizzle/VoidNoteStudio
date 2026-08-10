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

            if (settings.SchemaVersion is < 1 or > AppSettings.CurrentSchemaVersion)
            {
                return new AppSettings();
            }

            return Normalize(settings);
        }
        catch (JsonException exception)
        {
            System.Diagnostics.Debug.WriteLine($"VoidNote settings were invalid and defaults were loaded: {exception.Message}");
            return new AppSettings();
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var general = settings.General is null ? new GeneralSettings() : settings.General;
        var appearance = settings.Appearance is null ? new AppearanceSettings() : settings.Appearance;
        var autosave = settings.Autosave is null ? new AutosaveSettings() : settings.Autosave;
        var storage = settings.Storage is null ? new StorageSettings() : settings.Storage;
        var audio = settings.Audio is null ? new AudioSettings() : settings.Audio;
        var intelligence = settings.AudioIntelligence is null ? new AudioIntelligenceSettings() : settings.AudioIntelligence;
        var gameBridge = settings.GameBridge is null ? new GameBridgeSettings() : settings.GameBridge;
        var culture = general.Culture is "de" or "en" ? general.Culture : "en";
        var recent = (settings.RecentProjects ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value.Name) && !string.IsNullOrWhiteSpace(value.Path))
            .GroupBy(value => value.Path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(value => value.LastOpenedUtc).First())
            .OrderByDescending(value => value.LastOpenedUtc)
            .Take(12)
            .ToArray();
        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            General = general with { Culture = culture },
            Appearance = appearance,
            Autosave = autosave with { CustomIntervalMinutes = Math.Clamp(autosave.CustomIntervalMinutes, 1, 1440) },
            Storage = storage with { MigrationBackupRetention = Math.Clamp(storage.MigrationBackupRetention, 1, 10) },
            Audio = audio,
            GameBridge = gameBridge,
            AudioIntelligence = intelligence with
            {
                MaximumParallelJobs = Math.Clamp(intelligence.MaximumParallelJobs, 1, 4),
                WorkerTimeoutMinutes = Math.Clamp(intelligence.WorkerTimeoutMinutes, 1, 1440),
            },
            RecentProjects = recent,
        };
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
