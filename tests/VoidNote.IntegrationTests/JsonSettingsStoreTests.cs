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
}
