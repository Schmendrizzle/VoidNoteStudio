using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoidNote.App.ViewModels;
using VoidNote.App.Views;
using VoidNote.GameBridge.Playback;

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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.Exit += (_, _) =>
            {
                viewModel.AudioLab.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _services.GetRequiredService<GameBridgePlaybackSession>().StopAsync().GetAwaiter().GetResult();
            };
            _services.GetRequiredService<ILogger<App>>().LogInformation("VoidNote Studio Milestone H started.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
