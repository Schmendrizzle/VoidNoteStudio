using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Codec;

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

public sealed record ShawzinValidationPosition(
    int Index,
    int Pitch,
    string NoteName,
    ShawzinString String,
    ShawzinFret Fret,
    char CodeSymbol);

public sealed record ShawzinMappingValidationSequence(
    string InstrumentProfile,
    ShawzinScale Scale,
    IReadOnlyList<ShawzinValidationPosition> Positions,
    string Description,
    string SongCode);

public interface IShawzinValidationRecordStore
{
    Task<IReadOnlyList<ShawzinMappingValidationRecord>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ShawzinMappingValidationRecord record, CancellationToken cancellationToken = default);
}

public interface IShawzinValidationTool
{
    ShawzinCodeValidationReport Validate(string code, ShawzinDefinition instrument);
    string CreateMappingTestSequence(ShawzinDefinition instrument, ShawzinScale scale);
    ShawzinMappingValidationSequence CreateMappingValidation(ShawzinDefinition instrument, ShawzinScale scale);
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
        => CreateMappingValidation(instrument, scale).Description;

    public ShawzinMappingValidationSequence CreateMappingValidation(ShawzinDefinition instrument, ShawzinScale scale)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        var definition = instrument.Scales[scale];
        var positions = definition.Positions.Select(value => new ShawzinValidationPosition(
            value.PositionIndex + 1,
            value.Pitch,
            NoteName(value.Pitch),
            value.Input.String,
            value.Input.Frets,
            value.CodeSymbol)).ToArray();
        var events = definition.Positions.Select(value => new ShawzinEvent(
            StablePositionId(scale, value.PositionIndex),
            new AbsoluteTime(value.PositionIndex * 0.5m),
            new ShawzinChord([value.Input]))).ToList();
        var encoded = encoder.Encode(new ShawzinSong(new ShawzinTrack
        {
            Name = $"{definition.DisplayName} validation",
            InstrumentId = instrument.Id,
            Scale = scale,
            ShawzinEvents = events,
        }));
        if (!encoded.IsSuccess || encoded.Code is null)
            throw new InvalidOperationException("The deterministic twelve-note validation song could not be encoded.");
        var description = string.Join(Environment.NewLine, positions.Select(value =>
            $"{value.Index:00}. Pitch {value.Pitch} ({value.NoteName}) -> String {value.String}, Fret {value.Fret}, Symbol {value.CodeSymbol}"));
        return new(instrument.PlayProfile.Id, scale, positions, description, encoded.Code);
    }

    private static string NoteName(int pitch)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return $"{names[pitch % 12]}{pitch / 12 - 1}";
    }

    private static Guid StablePositionId(ShawzinScale scale, int position) =>
        Guid.Parse($"00000000-0000-0000-{(int)scale:x4}-{position + 1:x12}");

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
