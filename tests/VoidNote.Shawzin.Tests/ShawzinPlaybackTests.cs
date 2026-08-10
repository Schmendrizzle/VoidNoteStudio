using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Playback;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinPlaybackTests
{
    [Fact]
    public async Task Playback_UsesOneAbsoluteAnchorAndDistinguishesNotesFromChords()
    {
        var scheduler = new ImmediateScheduler();
        var output = new CollectingOutput();
        await using var engine = new ShawzinPlaybackEngine(scheduler, output);
        await engine.LoadAsync(new ShawzinTrack
        {
            ShawzinEvents =
            [
                Event(0m, ShawzinString.First),
                Event(1.25m, ShawzinString.First, ShawzinString.Second),
            ],
        });
        output.Reset();

        await engine.PlayAsync();

        Assert.Equal([0m, 1.25m], scheduler.Targets.Select(value => value.Seconds));
        Assert.Equal(1, output.Notes);
        Assert.Equal(1, output.Chords);
        Assert.Equal(1, output.Stops);
        Assert.Equal([0m, 1.25m], output.Positions.Select(value => value.Seconds));
    }

    private static ShawzinEvent Event(decimal seconds, params ShawzinString[] strings) => new(
        Guid.NewGuid(), new AbsoluteTime(seconds), new ShawzinChord(strings.Select(value => new ShawzinNote(value, ShawzinFret.None)).ToArray()));

    private sealed class ImmediateScheduler : IShawzinPlaybackScheduler
    {
        public List<AbsoluteTime> Targets { get; } = [];
        public long GetTimestamp() => 7;
        public AbsoluteTime GetElapsedTime(long startingTimestamp) => AbsoluteTime.Zero;
        public ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Targets.Add(targetOffset); return ValueTask.CompletedTask; }
    }

    private sealed class CollectingOutput : IShawzinPlaybackOutput
    {
        public int Notes { get; private set; }
        public int Chords { get; private set; }
        public int Stops { get; private set; }
        public List<AbsoluteTime> Positions { get; } = [];
        public void Reset() { Notes = 0; Chords = 0; Stops = 0; Positions.Clear(); }
        public ValueTask PlayNoteAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken) { Notes++; return ValueTask.CompletedTask; }
        public ValueTask PlayChordAsync(ShawzinEvent shawzinEvent, CancellationToken cancellationToken) { Chords++; return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken cancellationToken) { Stops++; return ValueTask.CompletedTask; }
        public ValueTask PositionChangedAsync(AbsoluteTime position, CancellationToken cancellationToken) { Positions.Add(position); return ValueTask.CompletedTask; }
    }
}
