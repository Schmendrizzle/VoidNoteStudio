using VoidNote.Domain.Music;

namespace VoidNote.Domain.Tests;

public sealed class ProjectTimelineTests
{
    [Fact]
    public void ConstantTempo_ConvertsTicksToExactSeconds()
    {
        var timeline = new ProjectTimeline(960, [new TempoChange(MusicalTime.Zero, 120m)]);

        var result = timeline.ToAbsoluteTime(new MusicalTime(3840));

        Assert.Equal(2m, result.Seconds);
    }

    [Fact]
    public void TempoMap_UsesEveryTempoSegment()
    {
        var timeline = new ProjectTimeline(960,
        [
            new TempoChange(MusicalTime.Zero, 120m),
            new TempoChange(new MusicalTime(1920), 60m),
        ]);

        var absolute = timeline.ToAbsoluteTime(new MusicalTime(2880));
        var roundTrip = timeline.ToMusicalTime(absolute);

        Assert.Equal(2m, absolute.Seconds);
        Assert.Equal(new MusicalTime(2880), roundTrip);
    }

    [Fact]
    public void Constructor_RejectsTempoMapsThatDoNotStartAtZero()
    {
        var changes = new[] { new TempoChange(new MusicalTime(1), 120m) };

        Assert.Throws<ArgumentException>(() => new ProjectTimeline(960, changes));
    }
}
