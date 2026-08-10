using VoidNote.Application.Settings;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Settings;

namespace VoidNote.IntegrationTests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSettingsStore(new AppPathProvider(directory.Path));

        var settings = await store.LoadAsync();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(ThemePreference.System, settings.Appearance.Theme);
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSettingsStore(new AppPathProvider(directory.Path));
        var expected = new AppSettings
        {
            General = new GeneralSettings { Culture = "de" },
            Appearance = new AppearanceSettings { Theme = ThemePreference.Dark },
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LoadAsync_MigratesVersionOneAndNormalizesInvalidValues()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppPathProvider(directory.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFilePath)!);
        await File.WriteAllTextAsync(paths.SettingsFilePath, """
            { "SchemaVersion": 1, "General": { "Culture": "invalid" },
              "AudioIntelligence": { "MaximumParallelJobs": 99, "WorkerTimeoutMinutes": 0 } }
            """);

        var settings = await new JsonSettingsStore(paths).LoadAsync();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal("en", settings.General.Culture);
        Assert.Equal(4, settings.AudioIntelligence.MaximumParallelJobs);
        Assert.Equal(1, settings.AudioIntelligence.WorkerTimeoutMinutes);
    }

    [Fact]
    public async Task LoadAsync_InvalidJsonFallsBackToSafeDefaults()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppPathProvider(directory.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFilePath)!);
        await File.WriteAllTextAsync(paths.SettingsFilePath, "{ not valid json");

        var settings = await new JsonSettingsStore(paths).LoadAsync();

        Assert.Equal(new AppSettings(), settings);
    }
}
