using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Application.Shawzin;

public sealed record ShawzinCodeValidationReport(
    bool IsValid,
    string ReEncodedCode,
    IReadOnlyList<string> Differences,
    string InstrumentProfile,
    int EventCount,
    decimal DurationSeconds,
    decimal? MinimumSpacingSeconds,
    decimal? MaximumSpacingSeconds,
    IReadOnlyList<string> Errors);

public sealed record ShawzinMappingValidationRecord(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string InstrumentId,
    ShawzinScale Scale,
    string TestSequence,
    bool UserConfirmed,
    string Notes);

public interface IShawzinValidationRecordStore
{
    Task<IReadOnlyList<ShawzinMappingValidationRecord>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ShawzinMappingValidationRecord record, CancellationToken cancellationToken = default);
}

public interface IShawzinValidationTool
{
    ShawzinCodeValidationReport Validate(string code, ShawzinDefinition instrument);
    string CreateMappingTestSequence(ShawzinDefinition instrument, ShawzinScale scale);
}

public sealed class ShawzinValidationTool(IShawzinCodeDecoder decoder, IShawzinCodeEncoder encoder, IShawzinCodeValidator validator) : IShawzinValidationTool
{
    public ShawzinCodeValidationReport Validate(string code, ShawzinDefinition instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        var validation = validator.Validate(code);
        var decoded = decoder.Decode(code);
        if (!validation.IsValid || !decoded.IsSuccess || decoded.Song is null)
        {
            var errors = validation.Errors.Concat(decoded.Errors).Select(value => $"{value.Category} at {value.CodePosition}: {value.Description}").Distinct().ToArray();
            return new(false, string.Empty, [], instrument.PlayProfile.Id, 0, 0, null, null, errors);
        }
        var encoded = encoder.Encode(decoded.Song);
        var reEncoded = encoded.Code ?? string.Empty;
        var differences = Compare(code, reEncoded);
        var events = decoded.Song.Track.ShawzinEvents.OrderBy(value => value.Position.Seconds).ToArray();
        var spacings = events.Zip(events.Skip(1), (left, right) => right.Position.Seconds - left.Position.Seconds).ToArray();
        return new(encoded.IsSuccess && differences.Count == 0, reEncoded, differences, instrument.PlayProfile.Id, events.Length,
            events.LastOrDefault()?.Position.Seconds ?? 0, spacings.Length == 0 ? null : spacings.Min(), spacings.Length == 0 ? null : spacings.Max(),
            encoded.Errors.Select(value => value.Description).ToArray());
    }

    public string CreateMappingTestSequence(ShawzinDefinition instrument, ShawzinScale scale)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        var definition = instrument.Scales[scale];
        return string.Join(Environment.NewLine, definition.Positions.OrderBy(value => value.Pitch).ThenBy(value => value.Input.String).ThenBy(value => value.Input.Frets)
            .Select((value, index) => $"{index + 1:00}. Pitch {value.Pitch} -> String {value.Input.String}, Frets {value.Input.Frets}"));
    }

    private static IReadOnlyList<string> Compare(string input, string output)
    {
        var differences = new List<string>();
        var maximum = Math.Max(input.Length, output.Length);
        for (var index = 0; index < maximum && differences.Count < 50; index++)
        {
            var left = index < input.Length ? input[index].ToString() : "<end>";
            var right = index < output.Length ? output[index].ToString() : "<end>";
            if (left != right) differences.Add($"Position {index}: input {left}, re-encoded {right}");
        }
        return differences;
    }
}
