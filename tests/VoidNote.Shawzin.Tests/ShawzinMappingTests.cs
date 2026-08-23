using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinMappingTests
{
    private readonly ShawzinDefinition _instrument = BuiltInShawzinDefinitions.Default;
    private readonly IShawzinPitchMapper _mapper = new ShawzinPitchMapper();

    [Fact]
    public void Definitions_ReusePlayProfileWhileKeepingSoundSeparate()
    {
        var dax = BuiltInShawzinDefinitions.Get("dax");
        var nelumbo = BuiltInShawzinDefinitions.Get("nelumbo");

        Assert.Same(dax.PlayProfile, nelumbo.PlayProfile);
        Assert.NotEqual(dax.SoundProfile.Id, nelumbo.SoundProfile.Id);
        Assert.Equal(9, dax.PlayProfile.Scales.Count);
        Assert.All(dax.PlayProfile.Scales.Values, scale => Assert.Equal(12, scale.Positions.Count));
    }

    [Fact]
    public void Mapper_ReportsOneExactRealPosition()
    {
        var result = _mapper.Map(67, _instrument, ShawzinScale.Chromatic);

        Assert.Equal(ShawzinPitchMappingKind.Exact, result.Kind);
        Assert.Single(result.Candidates);
        Assert.All(result.Candidates, candidate => Assert.Equal(67, candidate.Pitch));
    }

    [Fact]
    public void Mapper_DistinguishesRepairableAndOutOfRangePitch()
    {
        Assert.Equal(ShawzinPitchMappingKind.OctaveShiftable, _mapper.Map(48, _instrument, ShawzinScale.Chromatic).Kind);
        Assert.Equal(ShawzinPitchMappingKind.OutsideRange, _mapper.Map(1, _instrument, ShawzinScale.Major).Kind);
    }

    [Fact]
    public void Mapper_ReportsUnavailablePitchInsideDiatonicRange()
    {
        var result = _mapper.Map(66, _instrument, ShawzinScale.Major);
        Assert.Equal(ShawzinPitchMappingKind.NotAvailable, result.Kind);
    }
}
