using System.Runtime.InteropServices;
using VoidNote.GameBridge.Safety;

namespace VoidNote.GameBridge.Platform.Linux;

public sealed class LinuxX11TargetFocusService : IGameTargetFocusService
{
    public TargetFocusStatus GetStatus(string targetWindowTitle)
    {
        if (!OperatingSystem.IsLinux() || string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return new(false, false, "Reliable foreground-window checks are unavailable outside X11.");
        IntPtr display = IntPtr.Zero;
        try
        {
            display = XOpenDisplay(IntPtr.Zero); if (display == IntPtr.Zero) return new(false, false, "The X11 display could not be opened.");
            XGetInputFocus(display, out var window, out _);
            if (window == IntPtr.Zero || XFetchName(display, window, out var name) == 0 || name == IntPtr.Zero) return new(true, false, "The focused X11 window has no title.");
            try { var title = Marshal.PtrToStringAnsi(name) ?? string.Empty; var match = title.Contains(targetWindowTitle, StringComparison.OrdinalIgnoreCase); return new(true, match, match ? "The configured target window is focused." : $"Focused window is '{title}'."); }
            finally { XFree(name); }
        }
        catch (DllNotFoundException) { return new(false, false, "X11 libraries are unavailable."); }
        finally { if (display != IntPtr.Zero) XCloseDisplay(display); }
    }
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr displayName);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XGetInputFocus(IntPtr display, out IntPtr focus, out int revertTo);
    [DllImport("libX11.so.6")] private static extern int XFetchName(IntPtr display, IntPtr window, out IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XFree(IntPtr data);
}
