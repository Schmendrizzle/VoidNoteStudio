using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Validates the Warframe recorded-song V1 wire format and model.</summary>
public sealed class WarframeShawzinCodeValidator : IShawzinCodeValidator
{
    private readonly IShawzinCodeDecoder _decoder;

    /// <summary>Creates a validator.</summary>
    public WarframeShawzinCodeValidator(IShawzinCodeDecoder? decoder = null) =>
        _decoder = decoder ?? new WarframeShawzinCodeDecoder();

    /// <inheritdoc />
    public ShawzinValidationResult Validate(string? code)
    {
        var result = _decoder.Decode(code);
        return new ShawzinValidationResult(result.Errors);
    }

    /// <inheritdoc />
    public ShawzinValidationResult Validate(ShawzinSong? song)
    {
        if (song is null)
        {
            return Result(new ShawzinCodeError(
                ShawzinCodeErrorCategory.EmptyInput,
                "A Shawzin song cannot be null."));
        }

        var errors = new List<ShawzinCodeError>();
        if (!Enum.IsDefined(song.Scale))
        {
            errors.Add(new ShawzinCodeError(
                ShawzinCodeErrorCategory.InvalidScale,
                $"Scale value {(int)song.Scale} is not supported."));
        }

        var events = song.Track.ShawzinEvents;
        if (events.Count == 0)
        {
            errors.Add(new ShawzinCodeError(
                ShawzinCodeErrorCategory.EmptyInput,
                "A Shawzin song must contain at least one event."));
            return new ShawzinValidationResult(errors);
        }

        if (events.Count > WarframeShawzinCodeFormat.MaximumEventCount)
        {
            errors.Add(new ShawzinCodeError(
                ShawzinCodeErrorCategory.EventLimitExceeded,
                $"The song contains {events.Count} events; the structural maximum is {WarframeShawzinCodeFormat.MaximumEventCount}."));
        }

        var previousTimestamp = -1;
        for (var index = 0; index < events.Count; index++)
        {
            var current = events[index];
            int timestamp;
            try
            {
                timestamp = WarframeShawzinCodeFormat.QuantizeTimestamp(current.Position.Seconds);
            }
            catch (OverflowException)
            {
                timestamp = int.MaxValue;
            }

            if (timestamp is < 0 or > WarframeShawzinCodeFormat.MaximumTimestamp)
            {
                errors.Add(new ShawzinCodeError(
                    ShawzinCodeErrorCategory.InvalidTiming,
                    $"Event {index} quantizes outside the timestamp range 0-{WarframeShawzinCodeFormat.MaximumTimestamp}.",
                    EventIndex: index));
                continue;
            }

            if (timestamp <= previousTimestamp)
            {
                var category = current.Position.Seconds > events[index - 1].Position.Seconds
                    ? ShawzinCodeErrorCategory.QuantizationCollision
                    : ShawzinCodeErrorCategory.InvalidEventOrder;
                errors.Add(new ShawzinCodeError(
                    category,
                    category == ShawzinCodeErrorCategory.QuantizationCollision
                        ? $"Event {index} collides with the preceding event after timing quantization."
                        : $"Event {index} must occur after the preceding event.",
                    EventIndex: index));
            }

            previousTimestamp = timestamp;

            if (current.Chord.Notes.Count is < 1 or > 3 ||
                current.Chord.Notes.Select(note => note.String).Distinct().Count() != current.Chord.Notes.Count ||
                current.Chord.Notes.Select(note => note.Frets).Distinct().Count() != 1)
            {
                errors.Add(new ShawzinCodeError(
                    ShawzinCodeErrorCategory.InvalidModel,
                    $"Event {index} contains an unrepresentable chord.",
                    EventIndex: index));
            }
        }

        return new ShawzinValidationResult(errors);
    }

    private static ShawzinValidationResult Result(ShawzinCodeError error) => new([error]);
}
