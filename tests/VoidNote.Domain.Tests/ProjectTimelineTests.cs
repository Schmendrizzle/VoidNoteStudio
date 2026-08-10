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

    [Fact]
    public void Beats_RoundToNearestTickOnlyAtConversionBoundary()
    {
        var timeline = ProjectTimeline.CreateDefault();

        Assert.Equal(1.5m, timeline.ToBeats(new MusicalTime(1440)));
        Assert.Equal(new MusicalTime(320), timeline.FromBeats(1m / 3m));
    }

    [Fact]
    public void MusicalPosition_AccountsForTimeSignatureChanges()
    {
        var timeline = new ProjectTimeline(
            480,
            [new TempoChange(MusicalTime.Zero, 120m)],
            [
                new TimeSignatureChange(MusicalTime.Zero, 4, 4),
                new TimeSignatureChange(new MusicalTime(1920), 3, 4),
            ]);

        var position = timeline.ToMusicalPosition(new MusicalTime(3600));

        Assert.Equal(new MusicalPosition(3, 1, 240), position);
        Assert.Equal(new MusicalTime(3600), timeline.FromMusicalPosition(position));
    }

    [Fact]
    public void Bars_ConvertAcrossTimeSignatureSegments()
    {
        var timeline = new ProjectTimeline(
            480,
            [new TempoChange(MusicalTime.Zero, 120m)],
            [
                new TimeSignatureChange(MusicalTime.Zero, 4, 4),
                new TimeSignatureChange(new MusicalTime(1920), 3, 4),
            ]);

        Assert.Equal(2.5m, timeline.ToBars(new MusicalTime(4080)));
        Assert.Equal(new MusicalTime(4080), timeline.FromBars(2.5m));
    }

    [Fact]
    public void TimeSignature_RequiresPowerOfTwoDenominator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimeSignatureChange(MusicalTime.Zero, 4, 3));
    }
}
