using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Mapping;

/// <summary>Classifies a source pitch without applying a transformation.</summary>
public enum ShawzinPitchMappingKind
{
    Exact,
    NotAvailable,
    OutsideRange,
    OctaveShiftable,
}

/// <summary>Describes one playable target pitch and physical input.</summary>
public sealed record ShawzinPitchCandidate(int Pitch, ShawzinNote Input, int SemitoneDelta);

/// <summary>Contains the classification and every deterministic physical candidate.</summary>
public sealed record ShawzinPitchMappingResult(
    int SourcePitch,
    ShawzinPitchMappingKind Kind,
    IReadOnlyList<ShawzinPitchCandidate> Candidates)
{
    public bool IsPlayable => Kind == ShawzinPitchMappingKind.Exact;
}

/// <summary>Maps normalized pitches to data-driven Shawzin positions.</summary>
public interface IShawzinPitchMapper
{
    ShawzinPitchMappingResult Map(int pitch, ShawzinDefinition instrument, ShawzinScale scale);
    ShawzinPitchCandidate? FindClosest(int pitch, ShawzinDefinition instrument, ShawzinScale scale);
}

/// <summary>Maps normalized musical pitches through instrument data to physical Shawzin inputs.</summary>
public sealed class ShawzinPitchMapper : IShawzinPitchMapper
{
    public ShawzinPitchMappingResult Map(int pitch, ShawzinDefinition instrument, ShawzinScale scale)
    {
        if (pitch is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(pitch));
        ArgumentNullException.ThrowIfNull(instrument);
        var definition = GetScale(instrument, scale);
        var exact = definition.Positions
            .Where(value => value.Pitch == pitch)
            .Select(value => new ShawzinPitchCandidate(value.Pitch, value.Input, 0))
            .ToArray();
        if (exact.Length > 0) return new ShawzinPitchMappingResult(pitch, ShawzinPitchMappingKind.Exact, exact);

        var octave = definition.Positions
            .Where(value => value.Pitch % 12 == pitch % 12)
            .OrderBy(value => Math.Abs(value.Pitch - pitch))
            .ThenBy(value => value.Pitch)
            .ThenBy(value => value.Input.String)
            .Select(value => new ShawzinPitchCandidate(value.Pitch, value.Input, value.Pitch - pitch))
            .ToArray();
        var minimum = definition.Positions.Min(value => value.Pitch);
        var maximum = definition.Positions.Max(value => value.Pitch);
        var kind = octave.Length > 0
            ? ShawzinPitchMappingKind.OctaveShiftable
            : pitch < minimum || pitch > maximum
                ? ShawzinPitchMappingKind.OutsideRange
                : ShawzinPitchMappingKind.NotAvailable;
        return new ShawzinPitchMappingResult(pitch, kind, octave);
    }

    public ShawzinPitchCandidate? FindClosest(int pitch, ShawzinDefinition instrument, ShawzinScale scale) =>
        GetScale(instrument, scale).Positions
            .OrderBy(value => Math.Abs(value.Pitch - pitch))
            .ThenBy(value => value.Pitch)
            .ThenBy(value => value.Input.String)
            .ThenBy(value => value.Input.Frets)
            .Select(value => new ShawzinPitchCandidate(value.Pitch, value.Input, value.Pitch - pitch))
            .FirstOrDefault();

    private static ShawzinScaleDefinition GetScale(ShawzinDefinition instrument, ShawzinScale scale) =>
        instrument.PlayProfile.Scales.TryGetValue(scale, out var definition)
            ? definition
            : throw new ArgumentException($"Instrument '{instrument.Id}' does not support scale '{scale}'.", nameof(scale));
}
