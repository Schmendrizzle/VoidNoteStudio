using System.Runtime.InteropServices;
using VoidNote.GameBridge.Abstractions;

namespace VoidNote.GameBridge.Platform.Linux;

/// <summary>Uses X11 XTest on an existing user display; Wayland and missing XTest are explicitly unavailable.</summary>
public sealed class LinuxGameInputBridge : IGameInputBridge
{
    private readonly object _gate = new();
    private readonly HashSet<GameInputKey> _held = [];
    private IntPtr _display;
    public LinuxGameInputBridge()
    {
        Capability = Detect();
        if (Capability.IsAvailable) { try { _display = XOpenDisplay(IntPtr.Zero); } catch (DllNotFoundException) { _display = IntPtr.Zero; } }
        if (Capability.IsAvailable && _display == IntPtr.Zero) Capability = GameInputCapability.Unavailable("Linux X11/XTest", "The X11 display could not be opened.");
    }
    public GameInputCapability Capability { get; private set; }
    private static GameInputCapability Detect()
    {
        if (!OperatingSystem.IsLinux()) return GameInputCapability.Unavailable("Linux X11/XTest", "This backend requires Linux.");
        if (string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase))
            return GameInputCapability.Unavailable("Linux Wayland", "Synthetic global keyboard input is intentionally not attempted on Wayland.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return GameInputCapability.Unavailable("Linux X11/XTest", "No X11 DISPLAY is available.");
        if (!NativeLibrary.TryLoad("libX11.so.6", out var x11)) return GameInputCapability.Unavailable("Linux X11/XTest", "libX11 is not installed.");
        NativeLibrary.Free(x11);
        if (!NativeLibrary.TryLoad("libXtst.so.6", out var xtst)) return GameInputCapability.Unavailable("Linux X11/XTest", "The XTest extension library is not installed.");
        NativeLibrary.Free(xtst); return new(true, "Linux X11/XTest", "Documented X11 XTest keyboard simulation; no root access required.");
    }
    public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (_gate) if (_held.Add(key)) Send(key, true); return ValueTask.CompletedTask; }
    public async ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default)
    { foreach (var key in keys) await PressKeyAsync(key, eventId, cancellationToken); }
    public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (_gate) if (_held.Remove(key)) Send(key, false); return ValueTask.CompletedTask; }
    public async ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default)
    { await PressKeyAsync(key, eventId, cancellationToken); await Task.Delay(holdDuration, cancellationToken); await ReleaseKeyAsync(key, eventId, CancellationToken.None); }
    public async ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default)
    { GameInputKey[] keys; lock (_gate) keys = _held.ToArray(); foreach (var key in keys) await ReleaseKeyAsync(key, null, CancellationToken.None); }
    public async ValueTask DisposeAsync() { await ReleaseAllAsync(); if (_display != IntPtr.Zero) { XCloseDisplay(_display); _display = IntPtr.Zero; } }
    private void Send(GameInputKey key, bool down)
    {
        if (!Capability.IsAvailable) throw new InvalidOperationException(Capability.Description);
        var keysym = XStringToKeysym(ToX11Name(key.Name)); var code = XKeysymToKeycode(_display, keysym);
        if (keysym == IntPtr.Zero || code == 0) throw new ArgumentException($"Unsupported X11 key '{key}'.", nameof(key));
        if (XTestFakeKeyEvent(_display, code, down, 0) == 0) throw new InvalidOperationException($"XTest rejected key '{key}'.");
        XFlush(_display);
    }
    private static string ToX11Name(string name) => name.ToUpperInvariant() switch
    { "LEFT" => "Left", "RIGHT" => "Right", "UP" => "Up", "DOWN" => "Down", "SPACE" => "space", "ENTER" => "Return", "TAB" => "Tab", "ESCAPE" => "Escape", "SHIFT" => "Shift_L", "CONTROL" => "Control_L", "ALT" => "Alt_L", _ => name };
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr displayName);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern IntPtr XStringToKeysym([MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);
    [DllImport("libX11.so.6")] private static extern int XFlush(IntPtr display);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, [MarshalAs(UnmanagedType.Bool)] bool isPress, ulong delay);
}
