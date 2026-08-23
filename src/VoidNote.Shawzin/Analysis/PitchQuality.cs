using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Mapping;

namespace VoidNote.Shawzin.Analysis;

internal enum PitchQualityKind { Direct, Octave, Substitution, Dropped }

internal sealed record PitchQuality(PitchQualityKind Kind, int Distance, decimal Quality, int? TargetPitch)
{
    public static PitchQuality Evaluate(int pitch, IShawzinPitchMapper mapper, ShawzinDefinition instrument, ShawzinScale scale)
    {
        var mapping = mapper.Map(pitch, instrument, scale);
        if (mapping.Kind == ShawzinPitchMappingKind.Exact) return new(PitchQualityKind.Direct, 0, 1m, pitch);
        if (mapping.Kind == ShawzinPitchMappingKind.OctaveShiftable)
        {
            var distance = mapping.Candidates.Select(value => Math.Abs(value.SemitoneDelta)).DefaultIfEmpty(127).Min();
            var target = mapping.Candidates.OrderBy(value => Math.Abs(value.SemitoneDelta)).ThenBy(value => value.Pitch).First().Pitch;
            return new(PitchQualityKind.Octave, distance, Math.Max(0.4m, 0.7m - Math.Max(0, distance / 12 - 1) * 0.15m), target);
        }
        var closest = mapper.FindClosest(pitch, instrument, scale);
        if (closest is null) return new(PitchQualityKind.Dropped, 127, 0m, null);
        var substitutionDistance = Math.Abs(closest.SemitoneDelta);
        return new(PitchQualityKind.Substitution, substitutionDistance, Math.Max(0m, 0.30m - substitutionDistance * 0.05m), closest.Pitch);
    }

    public static PitchQuality EvaluateTransposed(int pitch, int semitones, IShawzinPitchMapper mapper, ShawzinDefinition instrument, ShawzinScale scale)
    {
        var transposed = pitch + semitones;
        if (transposed is < 0 or > 127) return new(PitchQualityKind.Dropped, 127, 0m, null);
        var quality = Evaluate(transposed, mapper, instrument, scale);
        var totalDistance = quality.TargetPitch is null ? 127 : Math.Abs(quality.TargetPitch.Value - pitch);
        var transpositionPenalty = Math.Abs(semitones) * 0.025m;
        return quality with { Distance = totalDistance, Quality = Math.Max(0m, quality.Quality - transpositionPenalty) };
    }
}
