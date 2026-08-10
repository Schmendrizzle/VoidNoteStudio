using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Decodes the fixed-width Warframe recorded-song V1 format.</summary>
public sealed class WarframeShawzinCodeDecoder : IShawzinCodeDecoder
{
    /// <inheritdoc />
    public ShawzinDecodeResult Decode(string? code)
    {
        try
        {
            return DecodeCore(code);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Failure(new ShawzinCodeError(
                ShawzinCodeErrorCategory.InvalidModel,
                $"The Shawzin code could not be decoded: {exception.Message}"));
        }
    }

    private static ShawzinDecodeResult DecodeCore(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return Failure(new ShawzinCodeError(
                ShawzinCodeErrorCategory.EmptyInput,
                "A Shawzin code cannot be empty."));
        }

        if (code[0] is < '1' or > '9')
        {
            return Failure(new ShawzinCodeError(
                ShawzinCodeErrorCategory.InvalidScale,
                "The scale at position 0 must be a digit from 1 through 9.",
                0,
                code[0]));
        }

        var payloadLength = code.Length - 1;
        if (payloadLength == 0 || payloadLength % WarframeShawzinCodeFormat.EventWidth != 0)
        {
            var incompleteEventPosition = 1 + payloadLength / WarframeShawzinCodeFormat.EventWidth * WarframeShawzinCodeFormat.EventWidth;
            return Failure(new ShawzinCodeError(
                ShawzinCodeErrorCategory.TruncatedCode,
                $"The code ends with an incomplete event at position {incompleteEventPosition}.",
                incompleteEventPosition));
        }

        var eventCount = payloadLength / WarframeShawzinCodeFormat.EventWidth;
        if (eventCount > WarframeShawzinCodeFormat.MaximumEventCount)
        {
            return Failure(new ShawzinCodeError(
                ShawzinCodeErrorCategory.EventLimitExceeded,
                $"The code contains {eventCount} events; the structural maximum is {WarframeShawzinCodeFormat.MaximumEventCount}."));
        }

        var events = new List<ShawzinEvent>(eventCount);
        var previousTimestamp = -1;
        for (var eventIndex = 0; eventIndex < eventCount; eventIndex++)
        {
            var position = 1 + eventIndex * WarframeShawzinCodeFormat.EventWidth;
            var noteSymbol = code[position];
            if (!WarframeShawzinCodeFormat.TryGetValue(noteSymbol, out var noteValue))
            {
                return Failure(new ShawzinCodeError(
                    ShawzinCodeErrorCategory.InvalidCharacter,
                    $"Invalid note character at position {position}.",
                    position,
                    noteSymbol,
                    eventIndex));
            }

            if ((noteValue & 0b111) == 0)
            {
                return Failure(new ShawzinCodeError(
                    ShawzinCodeErrorCategory.InvalidNoteSymbol,
                    $"The note symbol at position {position} does not strike a string.",
                    position,
                    noteSymbol,
                    eventIndex));
            }

            if (!TryTimingValue(code, position + 1, eventIndex, out var high, out var timingError) ||
                !TryTimingValue(code, position + 2, eventIndex, out var low, out timingError))
            {
                return Failure(timingError!);
            }

            var timestamp = high * 64 + low;
            if (timestamp <= previousTimestamp)
            {
                return Failure(new ShawzinCodeError(
                    ShawzinCodeErrorCategory.InvalidEventOrder,
                    $"Event {eventIndex} at position {position} must occur after the preceding event.",
                    position,
                    noteSymbol,
                    eventIndex));
            }

            previousTimestamp = timestamp;
            events.Add(new ShawzinEvent(
                Guid.NewGuid(),
                new AbsoluteTime(timestamp * WarframeShawzinCodeFormat.SecondsPerTimestamp),
                WarframeShawzinCodeFormat.DecodeChord(noteValue)));
        }

        var song = new ShawzinSong(new ShawzinTrack
        {
            Name = "Imported Shawzin Code",
            Scale = (ShawzinScale)(code[0] - '0'),
            ShawzinEvents = events,
        });
        return new ShawzinDecodeResult(song, []);
    }

    private static bool TryTimingValue(
        string code,
        int position,
        int eventIndex,
        out int value,
        out ShawzinCodeError? error)
    {
        if (WarframeShawzinCodeFormat.TryGetValue(code[position], out value))
        {
            error = null;
            return true;
        }

        error = new ShawzinCodeError(
            ShawzinCodeErrorCategory.InvalidTimingSymbol,
            $"Invalid timing symbol at position {position}.",
            position,
            code[position],
            eventIndex);
        return false;
    }

    private static ShawzinDecodeResult Failure(ShawzinCodeError error) => new(null, [error]);
}
