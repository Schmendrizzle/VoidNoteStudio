using VoidNote.Application.Shawzin;
using VoidNote.Domain.Shawzin;
using VoidNote.Infrastructure.Files;
using VoidNote.Infrastructure.Projects;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Definitions;

namespace VoidNote.IntegrationTests;

public sealed class ShawzinReleaseValidationTests
{
    [Fact]
    public void ValidationTool_DecodesAndReEncodesCanonicalCode()
    {
        var decoder = new WarframeShawzinCodeDecoder();
        var validator = new WarframeShawzinCodeValidator(decoder);
        var tool = new ShawzinValidationTool(decoder, new WarframeShawzinCodeEncoder(validator), validator);

        var result = tool.Validate("1BAA", BuiltInShawzinDefinitions.Default);

        Assert.True(result.IsValid);
        Assert.Equal("1BAA", result.ReEncodedCode);
        Assert.Equal(1, result.EventCount);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public async Task MappingValidationRecord_IsStoredLocallyAndBounded()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonShawzinValidationRecordStore(new AppPathProvider(directory.Path));
        var record = new ShawzinMappingValidationRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, "dax", ShawzinScale.Chromatic, "sequence", true, "manual");

        await store.SaveAsync(record);

        Assert.Equal(record, Assert.Single(await store.LoadAsync()));
    }

    [Fact]
    public void ValidationTool_GeneratesRealTwelvePositionTableAndSongCode()
    {
        var decoder = new WarframeShawzinCodeDecoder();
        var validator = new WarframeShawzinCodeValidator(decoder);
        var tool = new ShawzinValidationTool(decoder, new WarframeShawzinCodeEncoder(validator), validator);

        var sequence = tool.CreateMappingValidation(BuiltInShawzinDefinitions.Default, ShawzinScale.Chromatic);

        Assert.Equal(12, sequence.Positions.Count);
        Assert.Equal(Enumerable.Range(60, 12), sequence.Positions.Select(value => value.Pitch));
        Assert.Equal("BCEJKMRSUhik", string.Concat(sequence.Positions.Select(value => value.CodeSymbol)));
        Assert.StartsWith("3", sequence.SongCode);
        Assert.Equal(12, decoder.Decode(sequence.SongCode).Song!.Track.ShawzinEvents.Count);
    }
}
