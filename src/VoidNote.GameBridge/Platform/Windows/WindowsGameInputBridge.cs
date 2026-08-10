using System.ComponentModel;
using System.Runtime.InteropServices;
using VoidNote.GameBridge.Abstractions;

namespace VoidNote.GameBridge.Platform.Windows;

/// <summary>Uses documented Win32 SendInput keyboard events; it never opens or inspects a game process.</summary>
public sealed class WindowsGameInputBridge : IGameInputBridge
{
    private readonly object _gate = new();
    private readonly HashSet<GameInputKey> _held = [];
    public GameInputCapability Capability { get; } = OperatingSystem.IsWindows()
        ? new(true, "Windows SendInput", "Documented desktop keyboard input simulation.")
        : GameInputCapability.Unavailable("Windows SendInput", "This backend requires Windows.");

    public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (_gate) if (_held.Add(key)) Send(key, false); return ValueTask.CompletedTask; }
    public async ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default)
    { foreach (var key in keys) await PressKeyAsync(key, eventId, cancellationToken).ConfigureAwait(false); }
    public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (_gate) if (_held.Remove(key)) Send(key, true); return ValueTask.CompletedTask; }
    public async ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default)
    { await PressKeyAsync(key, eventId, cancellationToken); await Task.Delay(holdDuration, cancellationToken); await ReleaseKeyAsync(key, eventId, CancellationToken.None); }
    public async ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default)
    { GameInputKey[] keys; lock (_gate) keys = _held.ToArray(); foreach (var key in keys) await ReleaseKeyAsync(key, null, CancellationToken.None); }
    public async ValueTask DisposeAsync() => await ReleaseAllAsync();

    private void Send(GameInputKey key, bool up)
    {
        if (!Capability.IsAvailable) throw new InvalidOperationException(Capability.Description);
        var input = new Input { Type = 1, Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = Resolve(key), Flags = up ? 2u : 0u } } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not send key '{key}'.");
    }

    internal static ushort Resolve(GameInputKey key)
    {
        var name = key.Name.ToUpperInvariant();
        if (name.Length == 1 && name[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') return name[0];
        if (name.StartsWith('F') && int.TryParse(name[1..], out var f) && f is >= 1 and <= 12) return (ushort)(0x6F + f);
        return name switch { "LEFT" => 0x25, "UP" => 0x26, "RIGHT" => 0x27, "DOWN" => 0x28, "SPACE" => 0x20, "ENTER" => 0x0D, "TAB" => 0x09, "ESCAPE" => 0x1B, "SHIFT" => 0x10, "CONTROL" => 0x11, "ALT" => 0x12, _ => throw new ArgumentException($"Unsupported key '{key}'.", nameof(key)) };
    }

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Union; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int Dx; public int Dy; public uint MouseData; public uint Flags; public uint Time; public nuint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, [In] Input[] inputs, int size);
}
