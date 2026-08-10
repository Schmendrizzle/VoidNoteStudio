using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Facade for decoding, encoding, and validating the supported format variant.</summary>
public sealed class WarframeShawzinCodec : IShawzinCodec
{
    private readonly IShawzinCodeDecoder _decoder;
    private readonly IShawzinCodeEncoder _encoder;
    private readonly IShawzinCodeValidator _validator;

    /// <summary>Creates a facade from independently testable codec services.</summary>
    public WarframeShawzinCodec(
        IShawzinCodeDecoder? decoder = null,
        IShawzinCodeEncoder? encoder = null,
        IShawzinCodeValidator? validator = null)
    {
        _decoder = decoder ?? new WarframeShawzinCodeDecoder();
        _validator = validator ?? new WarframeShawzinCodeValidator(_decoder);
        _encoder = encoder ?? new WarframeShawzinCodeEncoder(_validator);
    }

    /// <inheritdoc />
    public ShawzinDecodeResult Decode(string? code) => _decoder.Decode(code);

    /// <inheritdoc />
    public ShawzinEncodeResult Encode(ShawzinSong? song) => _encoder.Encode(song);

    /// <inheritdoc />
    public ShawzinValidationResult Validate(string? code) => _validator.Validate(code);

    /// <inheritdoc />
    public ShawzinValidationResult Validate(ShawzinSong? song) => _validator.Validate(song);
}
