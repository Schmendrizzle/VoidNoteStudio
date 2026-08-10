using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoidNote.App.ViewModels;
using VoidNote.App.Views;
using VoidNote.GameBridge.Playback;
using VoidNote.Application.Settings;
using System.Globalization;
using VoidNote.Application.Jobs;
using VoidNote.Audio.Intelligence;

namespace VoidNote.App;

/// <summary>Initializes the Avalonia UI and resolves view models from the composition root.</summary>
public sealed class App(IServiceProvider services) : Avalonia.Application
{
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        ApplyAppearance(_services.GetRequiredService<AppSettings>());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.Exit += (_, _) => Shutdown(viewModel);
            _services.GetRequiredService<ILogger<App>>().LogInformation("VoidNote Studio {Version} started.", typeof(App).Assembly.GetName().Version);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Shutdown(MainWindowViewModel viewModel)
    {
        var logger = _services.GetRequiredService<ILogger<App>>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        RunCleanup("background jobs", () => _services.GetRequiredService<IBackgroundJobManager>().CancelAllAsync(timeout.Token), logger);
        RunCleanup("audio playback", () => viewModel.AudioLab.ShutdownAsync(), logger);
        RunCleanup("GameBridge", () => _services.GetRequiredService<GameBridgePlaybackSession>().StopAsync(), logger);
        RunCleanup("autosave and settings", () => viewModel.ShutdownAsync(timeout.Token), logger);
        RunCleanup("temporary AI resources", () => _services.GetRequiredService<IAudioIntelligenceTempManager>().CleanupOrphansAsync(TimeSpan.Zero, timeout.Token), logger);
    }

    private static void RunCleanup(string component, Func<Task> cleanup, ILogger logger)
    {
        try { cleanup().GetAwaiter().GetResult(); }
        catch (Exception exception) { logger.LogError(exception, "Shutdown cleanup failed for {Component}; remaining cleanup will continue.", component); }
    }

    private void ApplyAppearance(AppSettings settings)
    {
        var cultureName = settings.General.Culture is "de" ? "de" : "en";
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (Resources.MergedDictionaries.Count > 0) Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://VoidNote.App/"))
        {
            Source = new Uri($"avares://VoidNote.App/Resources/Strings.{cultureName}.axaml"),
        });
        RequestedThemeVariant = settings.Appearance.Theme switch
        {
            ThemePreference.Dark => ThemeVariant.Dark,
            ThemePreference.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
    }
}
