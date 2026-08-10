using System.Text.Json;
using VoidNote.Domain.Mandachord;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;
using VoidNote.Midi;

namespace VoidNote.Mandachord.Export;

public interface IMandachordMidiExporter { Task ExportAsync(Stream destination, MandachordPattern pattern, ProjectTimeline timeline, CancellationToken token = default); }
public sealed class MandachordMidiExporter(IMidiFileExporter exporter) : IMandachordMidiExporter
{
    public Task ExportAsync(Stream destination, MandachordPattern pattern, ProjectTimeline timeline, CancellationToken token = default)
    {
        pattern.Validate(); var stepTicks = timeline.TicksPerQuarterNote / 4;
        if (timeline.TicksPerQuarterNote % 4 != 0) throw new InvalidOperationException("Timeline PPQ cannot represent Mandachord sixteenth-note steps exactly.");
        var tracks = Enum.GetValues<MandachordLayer>().Select(layer => new MidiTrack { Name = $"Mandachord {layer}", Events = pattern.Steps.Where(value => value.Layer == layer).Select(value =>
        {
            var pitch = layer switch { MandachordLayer.Melody => MandachordGridDefinition.Standard.MelodyPitches[value.PitchPosition!.Value].PreviewMidiPitch, MandachordLayer.Bass => MandachordGridDefinition.Standard.BassPitches[value.PitchPosition!.Value].PreviewMidiPitch, _ => value.PercussionCategory switch { MandachordPercussionCategory.Kick => 36, MandachordPercussionCategory.Snare => 38, _ => 42 } };
            return new MusicalEvent(value.Id, new MusicalTime(value.StepIndex * stepTicks), new MusicalTime(stepTicks), pitch, value.Velocity, MusicalEventSource.Generated, 1m);
        }).ToList() }).ToArray();
        return exporter.ExportAsync(destination, timeline, tracks, token);
    }
}

public interface IMandachordJsonCodec { string Export(MandachordArrangement arrangement); MandachordArrangement Import(string json); }
public sealed class VoidNoteMandachordJsonCodec : IMandachordJsonCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General) { WriteIndented = true };
    public string Export(MandachordArrangement arrangement) { arrangement.Validate(); return JsonSerializer.Serialize(new Envelope("VoidNote Mandachord", 1, arrangement), Options); }
    public MandachordArrangement Import(string json)
    {
        var envelope = JsonSerializer.Deserialize<Envelope>(json, Options) ?? throw new InvalidDataException("Mandachord JSON is empty.");
        if (envelope.Format != "VoidNote Mandachord" || envelope.Version != 1) throw new InvalidDataException("This is not a supported VoidNote Mandachord JSON document.");
        envelope.Arrangement.Validate(); return envelope.Arrangement;
    }
    private sealed record Envelope(string Format, int Version, MandachordArrangement Arrangement);
}
