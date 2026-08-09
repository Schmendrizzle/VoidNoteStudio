namespace VoidNote.Application.Commands;

/// <summary>Provides deterministic linear undo and redo history.</summary>
public sealed class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    /// <inheritdoc />
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public string? UndoDescription => _undo.TryPeek(out var command) ? command.Description : null;

    /// <inheritdoc />
    public string? RedoDescription => _redo.TryPeek(out var command) ? command.Description : null;

    /// <inheritdoc />
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    /// <inheritdoc />
    public bool Undo()
    {
        if (!_undo.TryPeek(out var command))
        {
            return false;
        }

        command.Undo();
        _undo.Pop();
        _redo.Push(command);
        return true;
    }

    /// <inheritdoc />
    public bool Redo()
    {
        if (!_redo.TryPeek(out var command))
        {
            return false;
        }

        command.Execute();
        _redo.Pop();
        _undo.Push(command);
        return true;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
