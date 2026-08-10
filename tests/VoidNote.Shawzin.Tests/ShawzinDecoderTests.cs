using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Codec;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinDecoderTests
{
    private readonly IShawzinCodeDecoder _decoder = new WarframeShawzinCodeDecoder();

    [Fact]
    public void SingleNote_DecodesScaleChordAndExactTiming()
    {
        var result = _decoder.Decode(ShawzinFixture.Read("Valid", "single-note"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ShawzinScale.PentatonicMinor, result.Song!.Scale);
        var shawzinEvent = Assert.Single(result.Song.Track.ShawzinEvents);
        Assert.Equal(0m, shawzinEvent.Position.Seconds);
        var note = Assert.Single(shawzinEvent.Chord.Notes);
        Assert.Equal(ShawzinString.First, note.String);
        Assert.Equal(ShawzinFret.None, note.Frets);
    }

    [Fact]
    public void Chord_DecodesAllThreeStrings()
    {
        var result = _decoder.Decode(ShawzinFixture.Read("Valid", "chord"));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Song!.Track.ShawzinEvents[0].Chord.Notes.Count);
    }

    [Fact]
    public void LongPause_DecodesMaximumTimestampExactly()
    {
        var result = _decoder.Decode(ShawzinFixture.Read("Valid", "long-pause"));

        Assert.True(result.IsSuccess);
        Assert.Equal(255.9375m, result.Song!.Track.ShawzinEvents[^1].Position.Seconds);
    }

    [Fact]
    public void LongSong_DecodesEveryEventInOrder()
    {
        var result = _decoder.Decode(ShawzinFixture.Read("Valid", "long-song"));

        Assert.True(result.IsSuccess);
        Assert.Equal(256, result.Song!.Track.ShawzinEvents.Count);
        Assert.True(result.Song.Track.ShawzinEvents.Zip(result.Song.Track.ShawzinEvents.Skip(1))
            .All(pair => pair.First.Position.Seconds < pair.Second.Position.Seconds));
    }

    [Fact]
    public void InvalidTiming_ReportsCategoryPositionAndSymbol()
    {
        var result = _decoder.Decode(ShawzinFixture.Read("Invalid", "invalid-timing"));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ShawzinCodeErrorCategory.InvalidTimingSymbol, error.Category);
        Assert.Equal(3, error.CodePosition);
        Assert.Equal('?', error.Symbol);
        Assert.Equal("Invalid timing symbol at position 3.", error.Description);
    }

    [Fact]
    public void TruncatedCode_IsRejectedWithoutParserException()
    {
        var result = _decoder.Decode(ShawzinFixture.Read("Invalid", "truncated"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ShawzinCodeErrorCategory.TruncatedCode, Assert.Single(result.Errors).Category);
    }
}
