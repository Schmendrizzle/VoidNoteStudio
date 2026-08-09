namespace VoidNote.Application.Commands;

/// <summary>Represents one reversible in-memory editing operation.</summary>
public interface IUndoableCommand
{
    /// <summary>Gets a concise description suitable for command history.</summary>
    string Description { get; }

    /// <summary>Applies or reapplies the operation.</summary>
    void Execute();

    /// <summary>Reverts the applied operation.</summary>
    void Undo();
}
