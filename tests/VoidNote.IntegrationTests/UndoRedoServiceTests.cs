using VoidNote.Application.Commands;

namespace VoidNote.IntegrationTests;

public sealed class UndoRedoServiceTests
{
    [Fact]
    public void ExecuteUndoRedo_MovesCommandThroughLinearHistory()
    {
        var value = 0;
        var service = new UndoRedoService();
        var command = new DelegateCommand("Increment", () => value++, () => value--);

        service.Execute(command);
        Assert.Equal(1, value);
        Assert.Equal("Increment", service.UndoDescription);

        Assert.True(service.Undo());
        Assert.Equal(0, value);
        Assert.Equal("Increment", service.RedoDescription);

        Assert.True(service.Redo());
        Assert.Equal(1, value);
    }

    [Fact]
    public void NewCommand_ClearsRedoHistory()
    {
        var service = new UndoRedoService();
        service.Execute(new DelegateCommand("First", () => { }, () => { }));
        service.Undo();

        service.Execute(new DelegateCommand("Second", () => { }, () => { }));

        Assert.False(service.CanRedo);
    }

    private sealed record DelegateCommand(string Description, Action Apply, Action Revert) : IUndoableCommand
    {
        public void Execute() => Apply();
        public void Undo() => Revert();
    }
}
