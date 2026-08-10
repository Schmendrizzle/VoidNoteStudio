namespace VoidNote.GameBridge.Abstractions;

/// <summary>A portable keyboard key name, never a platform or Warframe key code.</summary>
public readonly record struct GameInputKey
{
    public GameInputKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
    public string Name { get; }
    public override string ToString() => Name;
}

public sealed record GameInputCapability(bool IsAvailable, string Backend, string Description)
{
    public static GameInputCapability Unavailable(string backend, string reason) => new(false, backend, reason);
}

/// <summary>Produces only ordinary OS keyboard input and owns every key it holds.</summary>
public interface IGameInputBridge : IAsyncDisposable
{
    GameInputCapability Capability { get; }
    ValueTask PressKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default);
    ValueTask ReleaseKeyAsync(GameInputKey key, Guid? eventId = null, CancellationToken cancellationToken = default);
    ValueTask TapKeyAsync(GameInputKey key, TimeSpan holdDuration, Guid? eventId = null, CancellationToken cancellationToken = default);
    ValueTask PressKeysAsync(IReadOnlyCollection<GameInputKey> keys, Guid? eventId = null, CancellationToken cancellationToken = default);
    ValueTask ReleaseAllAsync(CancellationToken cancellationToken = default);
}

public enum GameInputTransition { KeyDown, KeyUp }
public sealed record DiagnosticInputEvent(GameInputKey Key, GameInputTransition Transition, DateTimeOffset Timestamp, Guid? EventId);
