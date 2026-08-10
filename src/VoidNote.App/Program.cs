using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace VoidNote.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? typeof(Program).Assembly.GetName().Version?.ToString());
            return 0;
        }
        var services = CompositionRoot.BuildServiceProvider();
        try { return BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args); }
        finally { services.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .LogToTrace();
}
