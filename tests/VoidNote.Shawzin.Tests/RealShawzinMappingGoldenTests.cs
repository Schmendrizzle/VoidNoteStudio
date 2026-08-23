using System.Text.Json;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Tests;

public sealed class RealShawzinMappingGoldenTests
{
    private readonly ShawzinDefinition _instrument = BuiltInShawzinDefinitions.Default;
    private readonly ShawzinPitchMapper _mapper = new();

    [Fact]
    public void EveryScale_MatchesRealTwelvePositionGoldenFixture()
    {
        var fixture = JsonSerializer.Deserialize<MappingFixture>(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Mappings", "real-warframe-scales.json")), JsonOptions)!;

        Assert.Equal(fixture.ProfileId, _instrument.PlayProfile.Id);
        Assert.Equal(9, fixture.Scales.Length);
        foreach (var scaleFixture in fixture.Scales)
        {
            var scale = Enum.Parse<ShawzinScale>(scaleFixture.Scale);
            var actual = _instrument.Scales[scale].Positions;
            var codec = new WarframeShawzinCodec();
            Assert.Equal(12, actual.Count);
            for (var index = 0; index < 12; index++)
            {
                var physical = fixture.Positions[index];
                Assert.Equal(index, actual[index].PositionIndex);
                Assert.Equal(scaleFixture.Pitches[index], actual[index].Pitch);
                Assert.Equal(Enum.Parse<ShawzinString>(physical.String), actual[index].Input.String);
                Assert.Equal(Enum.Parse<ShawzinFret>(physical.Fret), actual[index].Input.Frets);
                Assert.Equal(Assert.Single(physical.Symbol), actual[index].CodeSymbol);
                Assert.Equal(actual[index].Pitch, _mapper.ReconstructPitch(actual[index].Input, _instrument, scale));
                var encoded = codec.Encode(new ShawzinSong(new ShawzinTrack
                {
                    Scale = scale,
                    ShawzinEvents = [new ShawzinEvent(Guid.NewGuid(), AbsoluteTime.Zero, new ShawzinChord([actual[index].Input]))],
                }));
                Assert.Equal(actual[index].CodeSymbol, encoded.Code![1]);
                var decodedInput = Assert.Single(Assert.Single(codec.Decode(encoded.Code).Song!.Track.ShawzinEvents).Chord.Notes);
                Assert.Equal(actual[index].Input, decodedInput);
                Assert.Equal(actual[index].Pitch, _mapper.ReconstructPitch(decodedInput, _instrument, scale));
            }
        }
    }

    [Fact]
    public void ChromaticFixture_IsDirectDeterministicAndUsesEveryRealPosition()
    {
        var source = Track(Enumerable.Range(0, 12).Select(index => Note(index * 120, 60 + index)).ToArray());
        var arranger = new ShawzinArranger(_mapper);
        var first = arranger.Arrange(source, Timeline, _instrument, new ArrangementOptions { Scale = ShawzinScale.Chromatic, Strategies = ArrangementStrategy.Strict });
        var second = arranger.Arrange(source, Timeline, _instrument, new ArrangementOptions { Scale = ShawzinScale.Chromatic, Strategies = ArrangementStrategy.Strict });

        Assert.True(first.IsSuccess);
        Assert.Equal(12, first.Report.ExactNoteCount);
        Assert.Equal(0, first.Report.OctaveShiftCount);
        Assert.Equal(0, first.Report.PitchSubstitutionCount);
        Assert.Equal(0, first.Report.DroppedNoteCount);
        Assert.Equal(100m, first.Report.MusicalSimilarity.OverallScore);
        Assert.Equal(Enumerable.Range(0, 12), first.Track!.ShawzinEvents.Select(value =>
            _instrument.Scales[ShawzinScale.Chromatic].Positions.Single(position => position.Input == Assert.Single(value.Chord.Notes)).PositionIndex));
        var encoder = new WarframeShawzinCodeEncoder();
        Assert.Equal(encoder.Encode(new ShawzinSong(first.Track)).Code, encoder.Encode(new ShawzinSong(second.Track!)).Code);
        Assert.Equal("3BAACABEACJADKAEMAFRAGSAHUAIhAJiAKkAL", encoder.Encode(new ShawzinSong(first.Track)).Code);
    }

    [Fact]
    public void MusicalFixtures_ArrangeWithoutPitchChanges()
    {
        var fixtures = JsonSerializer.Deserialize<MusicalFixture[]>(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Mappings", "musical-cases.json")), JsonOptions)!;
        Assert.Equal(4, fixtures.Length);
        foreach (var fixture in fixtures)
        {
            var scale = Enum.Parse<ShawzinScale>(fixture.Scale);
            var result = new ShawzinArranger(_mapper).Arrange(Track(fixture.Pitches.Select((pitch, index) => Note(index * 120, pitch)).ToArray()),
                Timeline, _instrument, new ArrangementOptions { Scale = scale, Strategies = ArrangementStrategy.Strict });
            Assert.True(result.IsSuccess, fixture.Name);
            Assert.Equal(fixture.Pitches, result.Track!.Events.Select(value => value.Pitch));
            Assert.Equal(100m, result.Report.MusicalSimilarity.OverallScore);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static ProjectTimeline Timeline { get; } = ProjectTimeline.CreateDefault();
    private static MidiTrack Track(params MusicalEvent[] notes) => new() { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Events = [.. notes] };
    private static MusicalEvent Note(long tick, int pitch) => new(Guid.Parse($"00000000-0000-0000-{pitch:x4}-{tick:x12}"), new MusicalTime(tick), new MusicalTime(120), pitch, 100, MusicalEventSource.ImportedMidi, 1m);
    private sealed record MappingFixture(string ProfileId, PhysicalFixture[] Positions, ScaleFixture[] Scales);
    private sealed record PhysicalFixture(int Index, string String, string Fret, string Symbol);
    private sealed record ScaleFixture(string Scale, int[] Pitches);
    private sealed record MusicalFixture(string Name, string Scale, int[] Pitches);
}
