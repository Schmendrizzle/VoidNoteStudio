using VoidNote.Domain.Music;

namespace VoidNote.Domain.Tests;

public sealed class MusicalEventTests
{
    [Fact]
    public void Constructor_PreservesTraceabilityFields()
    {
        var id = Guid.NewGuid();

        var musicalEvent = new MusicalEvent(
            id,
            new MusicalTime(120),
            new MusicalTime(240),
            64,
            100,
            MusicalEventSource.AudioTranscription,
            0.73m);

        Assert.Equal(id, musicalEvent.Id);
        Assert.Equal(MusicalEventSource.AudioTranscription, musicalEvent.Source);
        Assert.Equal(0.73m, musicalEvent.Confidence);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void Constructor_RejectsPitchOutsideNormalizedRange(int pitch)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MusicalEvent(
            Guid.NewGuid(), MusicalTime.Zero, MusicalTime.Zero, pitch, 100, MusicalEventSource.Manual, 1m));
    }
}
