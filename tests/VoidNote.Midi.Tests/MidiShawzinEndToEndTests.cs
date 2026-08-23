using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Mapping;
using VoidNote.Midi.Tests.Fixtures;

namespace VoidNote.Midi.Tests;

public sealed class MidiShawzinEndToEndTests
{
    [Fact]
    public async Task MidiImport_ToArrangement_ToValidSongCode_CompletesOffline()
    {
        await using var midi = SingleNoteMidi();
        var imported = await new DryWetMidiFileImporter().ImportAsync(midi);
        var arrangement = new ShawzinArranger(new ShawzinPitchMapper()).Arrange(
            Assert.Single(imported.Tracks), imported.Timeline, BuiltInShawzinDefinitions.Default,
            new ArrangementOptions { Scale = ShawzinScale.Chromatic, Strategies = ArrangementStrategy.Strict });

        Assert.True(arrangement.IsSuccess);
        var codec = new WarframeShawzinCodec();
        var encoded = codec.Encode(new ShawzinSong(arrangement.Track!));
        var decoded = codec.Decode(encoded.Code);

        Assert.True(encoded.IsSuccess);
        Assert.True(decoded.IsSuccess);
        Assert.Single(decoded.Song!.Track.ShawzinEvents);
        Assert.Equal(encoded.Code, codec.Encode(decoded.Song).Code);
    }

    [Fact]
    public async Task ChromaticMidi_ArrangeEncodeDecode_ReconstructsExactArrangedPitches()
    {
        await using var midi = MidiFixtureFactory.ChromaticValidation();
        var imported = await new DryWetMidiFileImporter().ImportAsync(midi);
        var mapper = new ShawzinPitchMapper();
        var instrument = BuiltInShawzinDefinitions.Default;
        var arrangement = new ShawzinArranger(mapper).Arrange(Assert.Single(imported.Tracks), imported.Timeline, instrument,
            new ArrangementOptions { Scale = ShawzinScale.Chromatic, Strategies = ArrangementStrategy.Strict });
        var codec = new WarframeShawzinCodec();
        var encoded = codec.Encode(new ShawzinSong(arrangement.Track!));
        var decoded = codec.Decode(encoded.Code);
        var reconstructed = decoded.Song!.Track.ShawzinEvents.SelectMany(value => value.Chord.Notes)
            .Select(value => mapper.ReconstructPitch(value, instrument, decoded.Song.Scale)).ToArray();

        Assert.True(arrangement.IsSuccess);
        Assert.True(encoded.IsSuccess);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(arrangement.Track!.Events.Select(value => value.Pitch), reconstructed);
        Assert.Equal(Enumerable.Range(60, 12), reconstructed);
        Assert.Equal(100m, arrangement.Report.MusicalSimilarity.OverallScore);
    }

    [Fact]
    public async Task SyntheticMonophonicMotif_PreservesOrderContourAndIntervals()
    {
        await using var midi = MidiFixtureFactory.SyntheticKnownMotif();
        var imported = await new DryWetMidiFileImporter().ImportAsync(midi);
        var arrangement = new ShawzinArranger(new ShawzinPitchMapper()).Arrange(Assert.Single(imported.Tracks), imported.Timeline,
            BuiltInShawzinDefinitions.Default, new ArrangementOptions { Scale = ShawzinScale.Major, Strategies = ArrangementStrategy.Strict });

        Assert.True(arrangement.IsSuccess);
        Assert.Equal(new[] { 60, 64, 67, 72, 71, 67, 64, 60 }, arrangement.Track!.Events.Select(value => value.Pitch));
        Assert.Equal(100m, arrangement.Report.MusicalSimilarity.MelodicContourPreservation);
        Assert.Equal(100m, arrangement.Report.MusicalSimilarity.IntervalPreservation);
    }

    private static MemoryStream SingleNoteMidi() => new(
    [
        0x4D,0x54,0x68,0x64, 0x00,0x00,0x00,0x06, 0x00,0x00, 0x00,0x01, 0x01,0xE0,
        0x4D,0x54,0x72,0x6B, 0x00,0x00,0x00,0x15,
        0x00,0xFF,0x03,0x04,0x4C,0x65,0x61,0x64,
        0x00,0x90,0x3C,0x64,
        0x83,0x60,0x80,0x3C,0x00,
        0x00,0xFF,0x2F,0x00,
    ]);
}
