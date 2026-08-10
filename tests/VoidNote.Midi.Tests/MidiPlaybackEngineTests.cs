using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Midi.Playback;

namespace VoidNote.Midi.Tests;

public sealed class MidiPlaybackEngineTests
{
    [Fact]
    public async Task Playback_SchedulesEveryEventAgainstOneAbsoluteAnchor()
    {
        var scheduler = new ImmediateScheduler();
        var output = new CollectingOutput();
        await using var engine = new MidiPlaybackEngine(scheduler, output);
        var timeline = new ProjectTimeline(480,
        [
            new TempoChange(MusicalTime.Zero, 120m),
            new TempoChange(new MusicalTime(480), 60m),
        ]);
        var track = TrackWith(new MusicalEvent(
            Guid.NewGuid(), MusicalTime.Zero, new MusicalTime(960), 60, 99, MusicalEventSource.Manual, 1m));

        await engine.LoadAsync(timeline, [track]);
        await engine.PlayAsync();

        Assert.Equal([0m, 1.5m], scheduler.TargetOffsets.Select(time => time.Seconds));
        Assert.Equal([ScheduledMidiEventKind.NoteOn, ScheduledMidiEventKind.NoteOff], output.Events.Select(item => item.Kind));
        Assert.Equal(MidiPlaybackState.Stopped, engine.State);
    }

    [Fact]
    public async Task SeekBeforePlay_SkipsEarlierEventsAndStopResetsPosition()
    {
        var scheduler = new ImmediateScheduler();
        var output = new CollectingOutput();
        await using var engine = new MidiPlaybackEngine(scheduler, output);
        var track = TrackWith(
            Note(0, 240, 60),
            Note(960, 240, 64));
        await engine.LoadAsync(ProjectTimeline.CreateDefault(), [track]);

        await engine.SeekAsync(new AbsoluteTime(0.5m));
        await engine.PlayAsync();

        Assert.DoesNotContain(output.Events, item => item.Pitch == 60);
        Assert.Equal(2, output.Events.Count(item => item.Pitch == 64));
        await engine.StopAsync();
        Assert.Equal(AbsoluteTime.Zero, engine.Position);
        Assert.True(output.AllNotesOffCount >= 2);
    }

    [Fact]
    public async Task CancelledPlay_EmitsNoEvents()
    {
        var scheduler = new ImmediateScheduler();
        var output = new CollectingOutput();
        await using var engine = new MidiPlaybackEngine(scheduler, output);
        await engine.LoadAsync(ProjectTimeline.CreateDefault(), [TrackWith(Note(0, 480, 60))]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await engine.PlayAsync(cancellation.Token);

        Assert.Empty(output.Events);
        Assert.Equal(MidiPlaybackState.Stopped, engine.State);
    }

    private static MidiTrack TrackWith(params MusicalEvent[] notes) =>
        new() { Name = "Playback", Events = [.. notes] };

    private static MusicalEvent Note(long start, long duration, int pitch) =>
        new(Guid.NewGuid(), new MusicalTime(start), new MusicalTime(duration), pitch, 100, MusicalEventSource.Manual, 1m);

    private sealed class ImmediateScheduler : IPlaybackScheduler
    {
        public List<AbsoluteTime> TargetOffsets { get; } = [];
        public long GetTimestamp() => 1;
        public AbsoluteTime GetElapsedTime(long startingTimestamp) => AbsoluteTime.Zero;
        public ValueTask WaitUntilAsync(long startingTimestamp, AbsoluteTime targetOffset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TargetOffsets.Add(targetOffset);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CollectingOutput : IMidiPlaybackOutput
    {
        public List<ScheduledMidiEvent> Events { get; } = [];
        public int AllNotesOffCount { get; private set; }
        public ValueTask SendAsync(ScheduledMidiEvent midiEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(midiEvent);
            return ValueTask.CompletedTask;
        }

        public ValueTask AllNotesOffAsync(CancellationToken cancellationToken)
        {
            AllNotesOffCount++;
            return ValueTask.CompletedTask;
        }
    }
}
