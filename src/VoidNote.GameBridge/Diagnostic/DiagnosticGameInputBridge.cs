using VoidNote.GameBridge.Abstractions;

namespace VoidNote.GameBridge.Diagnostic;

/// <summary>Records keyboard transitions without touching the host input system.</summary>
public sealed class DiagnosticGameInputBridge(TimeProvider? timeProvider = null) : IGameInputBridge
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly HashSet<GameInputKey> _held = [];
    private readonly List<DiagnosticInputEvent> _events = [];
    public GameInputCapability Capability { get; } = new(true, "Diagnostic", "No real input is emitted.");
    public IReadOnlyList<DiagnosticInputEvent> Events { get { lock (_gate) return _events.ToArray(); } }
    public IReadOnlyCollection<GameInputKey> HeldKeys { get { lock (_gate) return _held.ToArray(); } }

    public ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) if (_held.Add(key)) _events.Add(new(key, GameInputTransition.KeyDown, _timeProvider.GetUtcNow(), eventId));
        return ValueTask.CompletedTask;
    }

    public async ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys) await PressKeyAsync(key, eventId, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) if (_held.Remove(key)) _events.Add(new(key, GameInputTransition.KeyUp, _timeProvider.GetUtcNow(), eventId));
        return ValueTask.CompletedTask;
    }

    public async ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default)
    {
        await PressKeyAsync(key, eventId, cancellationToken).ConfigureAwait(false);
        if (holdDuration > TimeSpan.Zero) await Task.Delay(holdDuration, _timeProvider, cancellationToken).ConfigureAwait(false);
        await ReleaseKeyAsync(key, eventId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default)
    {
        GameInputKey[] keys; lock (_gate) keys = _held.ToArray();
        foreach (var key in keys) await ReleaseKeyAsync(key, null, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await ReleaseAllAsync().ConfigureAwait(false);
}
