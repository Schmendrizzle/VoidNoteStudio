namespace VoidNote.GameBridge.Safety;

public sealed record TargetFocusStatus(bool IsSupported, bool IsTargetFocused, string Description);
public interface IGameTargetFocusService { TargetFocusStatus GetStatus(string targetWindowTitle); }

public sealed class UnsupportedTargetFocusService(string description) : IGameTargetFocusService
{
    public TargetFocusStatus GetStatus(string targetWindowTitle) => new(false, false, description);
}

public enum GameBridgeArmState { Disarmed, Armed }

/// <summary>Explicit opt-in state; every terminal path returns to disarmed.</summary>
public sealed class GameBridgeArmController
{
    private readonly object _gate = new();
    private GameBridgeArmState _state;
    public GameBridgeArmState State { get { lock (_gate) return _state; } }
    public void Arm(bool disclaimerAcknowledged)
    {
        if (!disclaimerAcknowledged) throw new InvalidOperationException("The third-party software risk notice must be acknowledged first.");
        lock (_gate) _state = GameBridgeArmState.Armed;
    }
    public void Disarm() { lock (_gate) _state = GameBridgeArmState.Disarmed; }
    public void EnsureArmed() { if (State != GameBridgeArmState.Armed) throw new InvalidOperationException("GameBridge is disarmed."); }
}

public sealed class GameBridgeFocusException(string message) : InvalidOperationException(message);
