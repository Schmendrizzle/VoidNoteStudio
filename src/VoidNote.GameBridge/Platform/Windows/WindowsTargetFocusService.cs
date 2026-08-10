using System.Runtime.InteropServices;
using System.Text;
using VoidNote.GameBridge.Safety;

namespace VoidNote.GameBridge.Platform.Windows;

public sealed class WindowsTargetFocusService : IGameTargetFocusService
{
    public TargetFocusStatus GetStatus(string targetWindowTitle)
    {
        if (!OperatingSystem.IsWindows()) return new(false, false, "Windows foreground-window checks are unavailable.");
        var window = GetForegroundWindow(); if (window == IntPtr.Zero) return new(true, false, "No foreground window is available.");
        var title = new StringBuilder(512); _ = GetWindowText(window, title, title.Capacity);
        var matches = title.ToString().Contains(targetWindowTitle, StringComparison.OrdinalIgnoreCase);
        return new(true, matches, matches ? "The configured target window is focused." : $"Focused window is '{title}'.");
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
}
