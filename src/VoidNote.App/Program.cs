using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace VoidNote.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var services = CompositionRoot.BuildServiceProvider();
        try { return BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args); }
        finally { services.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .LogToTrace();
}
