using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Domain.Tests;

public sealed class ShawzinModelTests
{
    [Fact]
    public void Chord_CanonicalizesStringOrder()
    {
        var chord = new ShawzinChord(
        [
            new ShawzinNote(ShawzinString.Third, ShawzinFret.Sky),
            new ShawzinNote(ShawzinString.First, ShawzinFret.Sky),
        ]);

        Assert.Equal([ShawzinString.First, ShawzinString.Third], chord.Notes.Select(note => note.String));
    }

    [Fact]
    public void Chord_RejectsDifferentFretCombinations()
    {
        Assert.Throws<ArgumentException>(() => new ShawzinChord(
        [
            new ShawzinNote(ShawzinString.First, ShawzinFret.Sky),
            new ShawzinNote(ShawzinString.Second, ShawzinFret.Earth),
        ]));
    }

    [Fact]
    public void Event_ProjectsOntoSharedTimeline()
    {
        var shawzinEvent = new ShawzinEvent(
            Guid.NewGuid(),
            new AbsoluteTime(0.5m),
            new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]));

        var position = shawzinEvent.ToMusicalTime(ProjectTimeline.CreateDefault());

        Assert.Equal(960, position.Ticks);
    }
}
