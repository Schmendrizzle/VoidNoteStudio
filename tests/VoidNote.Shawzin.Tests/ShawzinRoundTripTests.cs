using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Codec;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinRoundTripTests
{
    private readonly IShawzinCodec _codec = new WarframeShawzinCodec();

    [Theory]
    [MemberData(nameof(ValidFixtureNames))]
    public void GoldenCode_DecodeEncode_IsByteForByteCanonical(string fixtureName)
    {
        var code = ShawzinFixture.Read("Valid", fixtureName);

        var decoded = _codec.Decode(code);
        var encoded = _codec.Encode(decoded.Song);

        Assert.True(decoded.IsSuccess);
        Assert.True(encoded.IsSuccess);
        Assert.Equal(code, encoded.Code);
        Assert.Empty(encoded.Quantizations);
    }

    [Fact]
    public void Song_EncodeDecode_PreservesScaleOrderTimingNotesAndChords()
    {
        var song = new ShawzinSong(
            new ShawzinTrack
            {
                Name = "Roundtrip",
                Scale = ShawzinScale.Hirajoshi,
                ShawzinEvents =
                [
                    Event(0m, ShawzinFret.Sky, ShawzinString.First),
                    Event(1.25m, ShawzinFret.Earth | ShawzinFret.Water, ShawzinString.Second, ShawzinString.Third),
                    Event(255.9375m, ShawzinFret.Sky | ShawzinFret.Earth | ShawzinFret.Water, ShawzinString.First, ShawzinString.Second, ShawzinString.Third),
                ],
            });

        var encoded = _codec.Encode(song);
        var decoded = _codec.Decode(encoded.Code);

        Assert.True(encoded.IsSuccess);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(song.Scale, decoded.Song!.Scale);
        Assert.Equal(
            song.Track.ShawzinEvents.Select(Semantics),
            decoded.Song.Track.ShawzinEvents.Select(Semantics));
    }

    [Fact]
    public void Encoder_QuantizesNearestTimestampAndReportsTransformation()
    {
        var song = Song(Event(0.04m, ShawzinFret.None, ShawzinString.First));

        var encoded = _codec.Encode(song);
        var decoded = _codec.Decode(encoded.Code);

        Assert.True(encoded.IsSuccess);
        var quantization = Assert.Single(encoded.Quantizations);
        Assert.Equal(0.0625m, quantization.EncodedSeconds);
        Assert.Equal(0.0625m, decoded.Song!.Track.ShawzinEvents[0].Position.Seconds);
    }

    [Fact]
    public void Encoder_IsDeterministic()
    {
        var song = Song(
            Event(0m, ShawzinFret.None, ShawzinString.First),
            Event(1m, ShawzinFret.Water, ShawzinString.Second));

        var outputs = Enumerable.Range(0, 20).Select(_ => _codec.Encode(song).Code).ToArray();

        Assert.All(outputs, output => Assert.Equal(outputs[0], output));
    }

    public static IEnumerable<object[]> ValidFixtureNames() => ShawzinFixture.ValidNames();

    private static ShawzinSong Song(params ShawzinEvent[] events) =>
        new(new ShawzinTrack { Scale = ShawzinScale.Chromatic, ShawzinEvents = [.. events] });

    private static ShawzinEvent Event(decimal seconds, ShawzinFret frets, params ShawzinString[] strings) =>
        new(
            Guid.NewGuid(),
            new AbsoluteTime(seconds),
            new ShawzinChord(strings.Select(value => new ShawzinNote(value, frets)).ToArray()));

    private static (decimal Time, ShawzinFret Frets, string Strings) Semantics(ShawzinEvent value) =>
        (value.Position.Seconds, value.Chord.Frets, string.Join(',', value.Chord.Notes.Select(note => (int)note.String)));
}
