using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Decodes the explicitly supported Warframe Shawzin song-code variant.</summary>
public interface IShawzinCodeDecoder
{
    /// <summary>Decodes a code without allowing parser exceptions to escape.</summary>
    ShawzinDecodeResult Decode(string? code);
}

/// <summary>Encodes a validated Shawzin song deterministically.</summary>
public interface IShawzinCodeEncoder
{
    /// <summary>Encodes a song and reports timing quantization.</summary>
    ShawzinEncodeResult Encode(ShawzinSong? song);
}

/// <summary>Validates both encoded text and the internal Shawzin model.</summary>
public interface IShawzinCodeValidator
{
    /// <summary>Validates encoded text.</summary>
    ShawzinValidationResult Validate(string? code);

    /// <summary>Validates an internal song before encoding.</summary>
    ShawzinValidationResult Validate(ShawzinSong? song);
}

/// <summary>Provides the complete Shawzin codec surface through one facade.</summary>
public interface IShawzinCodec : IShawzinCodeDecoder, IShawzinCodeEncoder, IShawzinCodeValidator;
