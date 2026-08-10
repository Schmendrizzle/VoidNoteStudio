using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Safety;

namespace VoidNote.GameBridge.Platform;

public static class PlatformGameInputBridgeFactory
{
    public static IGameInputBridge CreateBridge() =>
        OperatingSystem.IsWindows() ? new Windows.WindowsGameInputBridge() :
        OperatingSystem.IsLinux() ? new Linux.LinuxGameInputBridge() :
        new UnavailableGameInputBridge("Unsupported", "Only Windows and supported X11 Linux sessions provide real input.");

    public static IGameTargetFocusService CreateFocusService() =>
        OperatingSystem.IsWindows() ? new Windows.WindowsTargetFocusService() :
        OperatingSystem.IsLinux() ? new Linux.LinuxX11TargetFocusService() :
        new UnsupportedTargetFocusService("Foreground-window checks are unavailable on this platform.");
}

public sealed class UnavailableGameInputBridge(string backend, string reason) : IGameInputBridge
{
    public GameInputCapability Capability { get; } = GameInputCapability.Unavailable(backend, reason);
    private InvalidOperationException Error() => new(Capability.Description);
    public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
