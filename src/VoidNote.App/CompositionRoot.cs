using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoidNote.Application.Commands;
using VoidNote.Application.Projects;
using VoidNote.Application.Services;
using VoidNote.Application.Settings;
using VoidNote.App.ViewModels;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Logging;
using VoidNote.Infrastructure.Projects;
using VoidNote.Infrastructure.Settings;

namespace VoidNote.App;

internal static class CompositionRoot
{
    public static ServiceProvider BuildServiceProvider()
    {
        var paths = new AppPathProvider();
        var services = new ServiceCollection();

        services.AddSingleton<IAppPathProvider>(paths);
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IProjectStore, VnsProjectStore>();
        services.AddSingleton<IUndoRedoService, UndoRedoService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
            builder.AddProvider(new JsonFileLoggerProvider(
                Path.Combine(paths.LogDirectory, $"voidnote-{DateTime.UtcNow:yyyyMMdd}.log")));
        });

        return services.BuildServiceProvider(validateScopes: true);
    }
}
