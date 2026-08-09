namespace VoidNote.Application.Commands;

/// <summary>Coordinates reversible edit commands without depending on a UI framework.</summary>
public interface IUndoRedoService
{
    /// <summary>Gets whether an operation can be undone.</summary>
    bool CanUndo { get; }
    /// <summary>Gets whether an operation can be redone.</summary>
    bool CanRedo { get; }
    /// <summary>Gets the next command that can be undone.</summary>
    string? UndoDescription { get; }
    /// <summary>Gets the next command that can be redone.</summary>
    string? RedoDescription { get; }
    /// <summary>Executes a new command and clears redo history.</summary>
    void Execute(IUndoableCommand command);
    /// <summary>Undoes the latest command when available.</summary>
    bool Undo();
    /// <summary>Redoes the latest reverted command when available.</summary>
    bool Redo();
    /// <summary>Clears both histories.</summary>
    void Clear();
}
