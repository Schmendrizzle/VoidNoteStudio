using VoidNote.Application.Midi;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Midi.Tests;

public sealed class PianoRollViewModelTests
{
    [Fact]
    public void Projection_ProvidesTicksBeatsBarsAndAbsoluteTimeWithoutAvalonia()
    {
        var timeline = new ProjectTimeline(480, [new TempoChange(MusicalTime.Zero, 120m)]);
        var note = new MusicalEvent(Guid.NewGuid(), new MusicalTime(720), new MusicalTime(240), 69, 88, MusicalEventSource.Manual, 1m);
        var track = new MidiTrack { Name = "Piano", Events = [note] };

        var viewModel = new PianoRollViewModel(track, timeline);

        var projected = Assert.Single(viewModel.Notes);
        Assert.Equal(1.5m, projected.StartBeat);
        Assert.Equal(new MusicalPosition(1, 2, 240), projected.MusicalPosition);
        Assert.Equal(0.75m, projected.AbsoluteStart.Seconds);
        Assert.DoesNotContain(typeof(PianoRollViewModel).Assembly.GetReferencedAssemblies(), reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
    }
}
