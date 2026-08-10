using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Preview;

namespace VoidNote.Shawzin.Ensemble;

public interface IShawzinEnsemblePreviewRenderer
{
    ShawzinPreviewAudio Render(ShawzinEnsemble ensemble);
}

/// <summary>Creates a synthetic stereo mix without game audio assets or native audio dependencies.</summary>
public sealed class SyntheticShawzinEnsemblePreviewRenderer : IShawzinEnsemblePreviewRenderer
{
    private const int SampleRate = SyntheticShawzinPreviewRenderer.DefaultSampleRate;
    private const decimal TailSeconds = 0.35m;

    public ShawzinPreviewAudio Render(ShawzinEnsemble ensemble)
    {
        ArgumentNullException.ThrowIfNull(ensemble);
        var audible = ensemble.Tracks.Where(value => value.IsActive && !value.IsMuted && value.ShawzinTrack is not null).ToArray();
        if (audible.Any(value => value.IsSolo)) audible = audible.Where(value => value.IsSolo).ToArray();
        var duration = audible.SelectMany(value => value.ShawzinTrack!.ShawzinEvents).Select(value => value.Position.Seconds).DefaultIfEmpty(0m).Max();
        if (audible.Any(value => value.ShawzinTrack!.ShawzinEvents.Count > 0)) duration += TailSeconds;
        var frames = checked((int)decimal.Ceiling(duration * SampleRate));
        var left = new double[frames];
        var right = new double[frames];
        for (var trackIndex = 0; trackIndex < audible.Length; trackIndex++)
        {
            var track = audible[trackIndex];
            var pan = audible.Length <= 1 ? 0m : -0.6m + 1.2m * trackIndex / (audible.Length - 1m);
            var scale = track.Instrument.PlayProfile.Scales[track.Scale];
            var pitches = scale.Positions.GroupBy(value => value.Input).ToDictionary(value => value.Key, value => value.First().Pitch);
            var warm = track.Instrument.SoundProfile.PreviewPatch.Contains("Warm", StringComparison.OrdinalIgnoreCase);
            foreach (var shawzinEvent in track.ShawzinTrack!.ShawzinEvents)
            {
                var start = checked((int)decimal.Round(shawzinEvent.Position.Seconds * SampleRate, 0, MidpointRounding.AwayFromZero));
                foreach (var note in shawzinEvent.Chord.Notes)
                    if (pitches.TryGetValue(note, out var pitch)) AddTone(left, right, start, pitch, pan, warm);
            }
        }
        var peak = left.Concat(right).Select(Math.Abs).DefaultIfEmpty(0d).Max();
        var gain = peak > 0.95d ? 0.95d / peak : 1d;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataLength = checked(frames * 2 * sizeof(short));
        writer.Write("RIFF"u8); writer.Write(36 + dataLength); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((short)1); writer.Write((short)2); writer.Write(SampleRate); writer.Write(SampleRate * 4);
        writer.Write((short)4); writer.Write((short)16); writer.Write("data"u8); writer.Write(dataLength);
        for (var index = 0; index < frames; index++)
        {
            writer.Write((short)Math.Round(Math.Clamp(left[index] * gain, -1d, 1d) * short.MaxValue));
            writer.Write((short)Math.Round(Math.Clamp(right[index] * gain, -1d, 1d) * short.MaxValue));
        }
        return new ShawzinPreviewAudio(stream.ToArray(), SampleRate, duration);
    }

    private static void AddTone(double[] left, double[] right, int start, int pitch, decimal pan, bool warm)
    {
        var count = Math.Min(left.Length - start, (int)(SampleRate * TailSeconds));
        var frequency = 440d * Math.Pow(2d, (pitch - 69) / 12d);
        var leftGain = Math.Sqrt((1d - (double)pan) / 2d);
        var rightGain = Math.Sqrt((1d + (double)pan) / 2d);
        for (var index = 0; index < count; index++)
        {
            var time = index / (double)SampleRate;
            var envelope = Math.Exp(-(warm ? 6d : 8d) * time);
            var tone = Math.Sin(2d * Math.PI * frequency * time);
            if (warm) tone += 0.22d * Math.Sin(2d * Math.PI * frequency * 2d * time);
            tone *= envelope * 0.2d;
            left[start + index] += tone * leftGain;
            right[start + index] += tone * rightGain;
        }
    }
}
