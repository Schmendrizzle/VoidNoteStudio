using System.Text;
using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Deterministically encodes the Warframe recorded-song V1 format.</summary>
public sealed class WarframeShawzinCodeEncoder : IShawzinCodeEncoder
{
    private readonly IShawzinCodeValidator _validator;

    /// <summary>Creates an encoder.</summary>
    public WarframeShawzinCodeEncoder(IShawzinCodeValidator? validator = null) =>
        _validator = validator ?? new WarframeShawzinCodeValidator();

    /// <inheritdoc />
    public ShawzinEncodeResult Encode(ShawzinSong? song)
    {
        var validation = _validator.Validate(song);
        if (!validation.IsValid || song is null)
        {
            return new ShawzinEncodeResult(null, validation.Errors, []);
        }

        try
        {
            var code = new StringBuilder(1 + song.Track.ShawzinEvents.Count * WarframeShawzinCodeFormat.EventWidth);
            code.Append((char)('0' + (int)song.Scale));
            var quantizations = new List<ShawzinTimingQuantization>();
            for (var index = 0; index < song.Track.ShawzinEvents.Count; index++)
            {
                var shawzinEvent = song.Track.ShawzinEvents[index];
                var chord = WarframeShawzinCodeFormat.EncodeChord(shawzinEvent.Chord);
                var timestamp = WarframeShawzinCodeFormat.QuantizeTimestamp(shawzinEvent.Position.Seconds);
                code.Append(WarframeShawzinCodeFormat.GetSymbol(chord));
                code.Append(WarframeShawzinCodeFormat.GetSymbol(timestamp / 64));
                code.Append(WarframeShawzinCodeFormat.GetSymbol(timestamp % 64));

                var encodedSeconds = timestamp * WarframeShawzinCodeFormat.SecondsPerTimestamp;
                if (encodedSeconds != shawzinEvent.Position.Seconds)
                {
                    quantizations.Add(new ShawzinTimingQuantization(
                        shawzinEvent.Id,
                        index,
                        shawzinEvent.Position.Seconds,
                        encodedSeconds));
                }
            }

            return new ShawzinEncodeResult(code.ToString(), [], quantizations);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ShawzinEncodeResult(
                null,
                [new ShawzinCodeError(
                    ShawzinCodeErrorCategory.InvalidModel,
                    $"The Shawzin song could not be encoded: {exception.Message}")],
                []);
        }
    }
}
