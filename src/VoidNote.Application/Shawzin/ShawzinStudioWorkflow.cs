using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Domain.Shawzin;
using VoidNote.Midi;
using VoidNote.Shawzin.Analysis;
using VoidNote.Shawzin.Arrangement;
using VoidNote.Shawzin.Codec;
using VoidNote.Shawzin.Preview;

namespace VoidNote.Application.Shawzin;

/// <summary>Contains normalized tracks imported for the Shawzin Studio.</summary>
public sealed record ShawzinStudioImport(ProjectTimeline Timeline, IReadOnlyList<MidiTrack> Tracks);
/// <summary>Combines compatibility, scale and transposition suggestions.</summary>
public sealed record ShawzinStudioAnalysis(
    ShawzinCompatibilityReport Compatibility,
    IReadOnlyList<ShawzinScaleCandidate> ScaleCandidates,
    IReadOnlyList<ShawzinTranspositionCandidate> TranspositionCandidates);
/// <summary>Contains arrangement, encoding and preview results for the UI.</summary>
public sealed record ShawzinStudioArrangement(
    ShawzinArrangementResult Arrangement,
    ShawzinEncodeResult? Encoding,
    ShawzinPreviewAudio? Preview);

/// <summary>Defines the application-level MIDI-to-Shawzin workflow.</summary>
public interface IShawzinStudioWorkflow
{
    Task<ShawzinStudioImport> ImportMidiFileAsync(string path, CancellationToken cancellationToken = default);
    Task<ShawzinStudioImport> ImportMidiAsync(Stream source, CancellationToken cancellationToken = default);
    ShawzinStudioAnalysis Analyze(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale);
    ShawzinStudioArrangement Arrange(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ArrangementOptions options);
}

/// <summary>Coordinates the normalized MIDI-to-Shawzin studio flow outside the UI.</summary>
public sealed class ShawzinStudioWorkflow(
    IMidiFileImporter importer,
    IShawzinCompatibilityAnalyzer compatibilityAnalyzer,
    IShawzinScaleAnalyzer scaleAnalyzer,
    IShawzinTranspositionAnalyzer transpositionAnalyzer,
    IShawzinArranger arranger,
    IShawzinCodeEncoder encoder,
    IShawzinPreviewRenderer previewRenderer) : IShawzinStudioWorkflow
{
    public async Task<ShawzinStudioImport> ImportMidiFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ImportMidiAsync(source, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShawzinStudioImport> ImportMidiAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var result = await importer.ImportAsync(source, cancellationToken).ConfigureAwait(false);
        return new ShawzinStudioImport(result.Timeline, result.Tracks);
    }

    public ShawzinStudioAnalysis Analyze(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ShawzinScale scale) =>
        new(
            compatibilityAnalyzer.Analyze(track, timeline, instrument, scale),
            scaleAnalyzer.Analyze(track, instrument),
            transpositionAnalyzer.Analyze(track, timeline, instrument, scale));

    public ShawzinStudioArrangement Arrange(MidiTrack track, ProjectTimeline timeline, ShawzinDefinition instrument, ArrangementOptions options)
    {
        var result = arranger.Arrange(track, timeline, instrument, options);
        if (result.Track is null) return new ShawzinStudioArrangement(result, null, null);
        var encoding = encoder.Encode(new ShawzinSong(result.Track));
        var preview = previewRenderer.Render(result.Track, instrument);
        return new ShawzinStudioArrangement(result, encoding, preview);
    }
}
