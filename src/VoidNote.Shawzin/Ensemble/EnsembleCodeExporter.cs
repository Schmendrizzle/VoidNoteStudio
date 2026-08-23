using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Codec;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>One independently validated creator-code export.</summary>
public sealed record EnsembleTrackExport(
    Guid TrackId,
    string TrackName,
    string Instrument,
    ShawzinScale Scale,
    int TranspositionSemitones,
    int EventCount,
    decimal DurationSeconds,
    int Compatibility,
    decimal MusicalSimilarity,
    string? Code,
    int CodeLength,
    bool IsValid,
    IReadOnlyList<ShawzinCodeError> Errors);

/// <summary>Contains all independent track codes and their validation status.</summary>
public sealed record EnsembleExportReport(IReadOnlyList<EnsembleTrackExport> Tracks)
{
    public bool IsValid => Tracks.Count > 0 && Tracks.All(value => value.IsValid);
}

public interface IEnsembleCodeExporter
{
    EnsembleExportReport Export(ShawzinEnsemble ensemble);
}

/// <summary>Encodes each ensemble member through the existing Recorded-Song-V1 codec.</summary>
public sealed class EnsembleCodeExporter(IShawzinCodeEncoder encoder) : IEnsembleCodeExporter
{
    public EnsembleExportReport Export(ShawzinEnsemble ensemble)
    {
        ArgumentNullException.ThrowIfNull(ensemble);
        var exports = ensemble.Tracks.Select(track =>
        {
            if (track.ShawzinTrack is null)
                return new EnsembleTrackExport(track.Id, track.DisplayName, track.Instrument.DisplayName, track.Scale,
                    track.TranspositionSemitones, 0, 0m, track.Compatibility?.OverallScore ?? 0, track.MusicalSimilarity, null, 0, false,
                    [new ShawzinCodeError(ShawzinCodeErrorCategory.InvalidModel, "The track has no successful arrangement.")]);
            var result = encoder.Encode(new ShawzinSong(track.ShawzinTrack));
            var duration = track.ShawzinTrack.ShawzinEvents.LastOrDefault()?.Position.Seconds ?? 0m;
            return new EnsembleTrackExport(track.Id, track.DisplayName, track.Instrument.DisplayName, track.Scale,
                track.TranspositionSemitones, track.ShawzinTrack.ShawzinEvents.Count, duration,
                track.Compatibility?.OverallScore ?? 0, track.MusicalSimilarity, result.Code, result.Code?.Length ?? 0, result.IsSuccess, result.Errors);
        }).ToArray();
        return new EnsembleExportReport(exports);
    }
}
