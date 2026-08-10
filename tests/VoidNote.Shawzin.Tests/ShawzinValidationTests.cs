using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Codec;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinValidationTests
{
    private readonly IShawzinCodeValidator _validator = new WarframeShawzinCodeValidator();

    [Theory]
    [InlineData("invalid-character", ShawzinCodeErrorCategory.InvalidCharacter)]
    [InlineData("invalid-timing", ShawzinCodeErrorCategory.InvalidTimingSymbol)]
    [InlineData("truncated", ShawzinCodeErrorCategory.TruncatedCode)]
    [InlineData("invalid-event-order", ShawzinCodeErrorCategory.InvalidEventOrder)]
    [InlineData("silent-note", ShawzinCodeErrorCategory.InvalidNoteSymbol)]
    public void InvalidGoldenCode_IsRejectedWithExpectedCategory(
        string fixtureName,
        ShawzinCodeErrorCategory category)
    {
        var result = _validator.Validate(ShawzinFixture.Read("Invalid", fixtureName));

        Assert.False(result.IsValid);
        Assert.Equal(category, Assert.Single(result.Errors).Category);
    }

    [Fact]
    public void ModelWithQuantizationCollision_IsRejected()
    {
        var song = Song(Event(0m), Event(0.02m));

        var result = _validator.Validate(song);

        Assert.Contains(result.Errors, error => error.Category == ShawzinCodeErrorCategory.QuantizationCollision);
    }

    [Fact]
    public void ModelBeyondMaximumTiming_IsRejected()
    {
        var song = Song(Event(256m));

        var result = _validator.Validate(song);

        Assert.Contains(result.Errors, error => error.Category == ShawzinCodeErrorCategory.InvalidTiming);
    }

    [Fact]
    public void ModelEventsMustBeStrictlyOrdered()
    {
        var song = Song(Event(1m), Event(0m));

        var result = _validator.Validate(song);

        Assert.Contains(result.Errors, error => error.Category == ShawzinCodeErrorCategory.InvalidEventOrder);
    }

    private static ShawzinSong Song(params ShawzinEvent[] events) =>
        new(new ShawzinTrack { Scale = ShawzinScale.Major, ShawzinEvents = [.. events] });

    private static ShawzinEvent Event(decimal seconds) =>
        new(
            Guid.NewGuid(),
            new AbsoluteTime(seconds),
            new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]));
}
