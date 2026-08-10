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
using VoidNote.Shawzin.Ensemble;
using VoidNote.Application.Jobs;
using VoidNote.Audio.Decoding;
using VoidNote.Audio.Import;
using VoidNote.Audio.Playback;
using VoidNote.Audio.Waveforms;
using VoidNote.Audio.Intelligence;
using VoidNote.Application.Creator;
using VoidNote.Application.Mandachord;
using VoidNote.Mandachord.Mapping;
using VoidNote.Mandachord.Generation;
using VoidNote.Mandachord.Preview;
using VoidNote.Mandachord.Export;

namespace VoidNote.App;

internal static class CompositionRoot
{
    public static ServiceProvider BuildServiceProvider()
    {
        var paths = new AppPathProvider();
        AppSettings startupSettings;
        try { startupSettings = new JsonSettingsStore(paths).LoadAsync().GetAwaiter().GetResult(); }
        catch (Exception exception) when (exception is IOException or InvalidDataException) { startupSettings = new AppSettings(); }
        var services = new ServiceCollection();

        services.AddSingleton<IAppPathProvider>(paths);
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IProjectStore, VnsProjectStore>();
        services.AddSingleton<IUndoRedoService, UndoRedoService>();
        services.AddSingleton<IBackgroundJobManager, BackgroundJobManager>();
        services.AddSingleton<WaveAudioDecoder>();
        services.AddSingleton(serviceProvider => new FfmpegAudioDecoder(
            serviceProvider.GetRequiredService<ILogger<FfmpegAudioDecoder>>(),
            startupSettings.Audio.FfmpegExecutablePath ?? "ffmpeg",
            startupSettings.Audio.FfprobeExecutablePath ?? "ffprobe"));
        services.AddSingleton<IAudioDecoder, PlatformAudioDecoder>();
        services.AddSingleton<IAudioImportService, AudioImportService>();
        services.AddSingleton<IWaveformCache>(_ => new FileWaveformCache(paths.WaveformCacheDirectory,
            _.GetRequiredService<ILogger<FileWaveformCache>>()));
        services.AddSingleton<IWaveformGenerator, WaveformGenerator>();
        services.AddSingleton<IAudioPlaybackClock, SystemAudioPlaybackClock>();
        services.AddSingleton<IAudioDeviceProvider>(serviceProvider => new PlatformAudioDeviceProvider(
            serviceProvider.GetRequiredService<ILoggerFactory>(), startupSettings.Audio.FfplayExecutablePath ?? "ffplay"));
        services.AddTransient<AudioPlaybackEngine>();
        services.AddTransient<AudioStemMixPreview>();
        var workerScript = startupSettings.AudioIntelligence.WorkerScriptPath ?? Path.Combine(AppContext.BaseDirectory, "workers", "python", "voidnote_ai_worker.py");
        services.AddSingleton<IAudioWorkerClient>(serviceProvider => new ProcessAudioWorkerClient(
            startupSettings.AudioIntelligence.PythonExecutablePath ?? "python", workerScript,
            TimeSpan.FromMinutes(Math.Max(1, startupSettings.AudioIntelligence.WorkerTimeoutMinutes)),
            serviceProvider.GetRequiredService<ILogger<ProcessAudioWorkerClient>>()));
        services.AddSingleton<IAudioSeparationEngine, DemucsSeparationEngine>();
        services.AddSingleton<IAudioTranscriptionEngine, BasicPitchTranscriptionEngine>();
        var intelligenceTemp = new AudioIntelligenceTempManager(paths.AudioIntelligenceTempDirectory);
        try { intelligenceTemp.CleanupOrphansAsync(TimeSpan.FromDays(1)).GetAwaiter().GetResult(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        services.AddSingleton<IAudioIntelligenceTempManager>(intelligenceTemp);
        services.AddSingleton<IAiResourceGate>(_ => new AiResourceGate(Math.Max(1, startupSettings.AudioIntelligence.MaximumParallelJobs)));
        services.AddSingleton<IAudioIntelligenceWorkflow>(serviceProvider => new AudioIntelligenceWorkflow(
            serviceProvider.GetRequiredService<IAudioSeparationEngine>(), serviceProvider.GetRequiredService<IAudioTranscriptionEngine>(),
            serviceProvider.GetRequiredService<IAudioDecoder>(), serviceProvider.GetRequiredService<IAudioIntelligenceTempManager>(),
            serviceProvider.GetRequiredService<IAiResourceGate>(), paths.AudioIntelligenceAssetDirectory));
        services.AddTransient<AudioLabViewModel>();
        services.AddSingleton<IMidiFileImporter, DryWetMidiFileImporter>();
        services.AddSingleton<IShawzinPitchMapper, ShawzinPitchMapper>();
        services.AddSingleton<IShawzinCompatibilityAnalyzer, ShawzinCompatibilityAnalyzer>();
        services.AddSingleton<IShawzinScaleAnalyzer, ShawzinScaleAnalyzer>();
        services.AddSingleton<IShawzinTranspositionAnalyzer, ShawzinTranspositionAnalyzer>();
        services.AddSingleton<IShawzinArranger, ShawzinArranger>();
        services.AddSingleton<IShawzinCodeEncoder, WarframeShawzinCodeEncoder>();
        services.AddSingleton<IShawzinPreviewRenderer, SyntheticShawzinPreviewRenderer>();
        services.AddSingleton<IShawzinStudioWorkflow, ShawzinStudioWorkflow>();
        services.AddSingleton<VoiceSalienceAnalyzer>();
        services.AddSingleton<IMultiShawzinSplitter, MultiShawzinSplitter>();
        services.AddSingleton<IShawzinEnsembleArranger, ShawzinEnsembleArranger>();
        services.AddSingleton<IEnsembleCodeExporter, EnsembleCodeExporter>();
        services.AddSingleton<IShawzinEnsemblePreviewRenderer, SyntheticShawzinEnsemblePreviewRenderer>();
        services.AddSingleton<IMultiShawzinWorkflow, MultiShawzinWorkflow>();
        services.AddSingleton<IEnsembleReassignmentService, EnsembleReassignmentService>();
        services.AddSingleton<ICreatorTimingService, CreatorTimingService>();
        services.AddSingleton<ICreatorSessionFactory, CreatorSessionFactory>();
        services.AddSingleton<ICreatorExportService, CreatorExportService>();
        services.AddTransient<CreatorModeViewModel>();
        services.AddSingleton<IMandachordPitchMapper, MandachordPitchMapper>();
        services.AddSingleton<IMandachordTimingMapper, MandachordTimingMapper>();
        services.AddSingleton<IMandachordGenerator, MandachordGenerator>();
        services.AddSingleton<IMandachordPreviewRenderer, SyntheticMandachordPreviewRenderer>();
        services.AddSingleton<ICombinedPreviewRenderer, PcmCombinedPreviewRenderer>();
        services.AddSingleton<IMandachordJsonCodec, VoidNoteMandachordJsonCodec>();
        services.AddSingleton<IMandachordMidiExporter, MandachordMidiExporter>();
        services.AddSingleton<IMandachordEditorService, MandachordEditorService>();
        services.AddTransient<MandachordStudioViewModel>();
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
