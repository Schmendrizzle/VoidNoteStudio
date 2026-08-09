using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace VoidNote.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var services = CompositionRoot.BuildServiceProvider();
        return BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .LogToTrace();
}
