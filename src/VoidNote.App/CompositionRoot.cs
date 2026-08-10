using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoidNote.Application.Commands;
using VoidNote.Application.Projects;
using VoidNote.Application.Services;
using VoidNote.Application.Settings;
using VoidNote.Application.Shawzin;
using VoidNote.App.ViewModels;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Logging;
using VoidNote.Infrastructure.Projects;
using VoidNote.Infrastructure.Settings;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Mapping;
using VoidNote.GameBridge.Platform;
using VoidNote.GameBridge.Playback;
using VoidNote.GameBridge.Profiles;
using VoidNote.GameBridge.Safety;
using VoidNote.Midi;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Mapping;
using VoidNote.Shawzin.Preview;

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
        services.AddSingleton<IMidiFileImporter, DryWetMidiFileImporter>();
        services.AddSingleton<IShawzinPitchMapper, ShawzinPitchMapper>();
        services.AddSingleton<IShawzinCompatibilityAnalyzer, ShawzinCompatibilityAnalyzer>();
        services.AddSingleton<IShawzinScaleAnalyzer, ShawzinScaleAnalyzer>();
        services.AddSingleton<IShawzinTranspositionAnalyzer, ShawzinTranspositionAnalyzer>();
        services.AddSingleton<IShawzinArranger, ShawzinArranger>();
        services.AddSingleton<IShawzinCodeEncoder, WarframeShawzinCodeEncoder>();
        services.AddSingleton<IShawzinPreviewRenderer, SyntheticShawzinPreviewRenderer>();
        services.AddSingleton<IShawzinStudioWorkflow, ShawzinStudioWorkflow>();
        services.AddSingleton<IKeybindProfileValidator, KeybindProfileValidator>();
        services.AddSingleton<IKeybindProfileStore, JsonKeybindProfileStore>();
        services.AddSingleton<KeybindProfileService>();
        services.AddSingleton<IShawzinInputMapper, ShawzinInputMapper>();
        services.AddSingleton<GameBridgeArmController>();
        services.AddSingleton<IGameInputBridge>(_ => PlatformGameInputBridgeFactory.CreateBridge());
        services.AddSingleton<IGameTargetFocusService>(_ => PlatformGameInputBridgeFactory.CreateFocusService());
        services.AddSingleton<GameBridgePlaybackSession>();
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
