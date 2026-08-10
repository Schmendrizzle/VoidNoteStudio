using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Contains either a decoded song or structured parser errors.</summary>
public sealed record ShawzinDecodeResult(ShawzinSong? Song, IReadOnlyList<ShawzinCodeError> Errors)
{
    /// <summary>Gets whether decoding succeeded.</summary>
    public bool IsSuccess => Song is not null && Errors.Count == 0;
}

/// <summary>Describes one event position changed at the Shawzin timing boundary.</summary>
public sealed record ShawzinTimingQuantization(
    Guid EventId,
    int EventIndex,
    decimal OriginalSeconds,
    decimal EncodedSeconds);

/// <summary>Contains either an encoded code or structured model errors.</summary>
public sealed record ShawzinEncodeResult(
    string? Code,
    IReadOnlyList<ShawzinCodeError> Errors,
    IReadOnlyList<ShawzinTimingQuantization> Quantizations)
{
    /// <summary>Gets whether encoding succeeded.</summary>
    public bool IsSuccess => Code is not null && Errors.Count == 0;
}
