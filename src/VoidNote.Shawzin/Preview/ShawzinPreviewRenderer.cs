using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Preview;

/// <summary>Contains rendered WAV data and its timing metadata.</summary>
public sealed record ShawzinPreviewAudio(byte[] WaveData, int SampleRate, decimal DurationSeconds);

/// <summary>Renders a Shawzin track independently of arrangement and transport.</summary>
public interface IShawzinPreviewRenderer
{
    ShawzinPreviewAudio Render(ShawzinTrack track, ShawzinDefinition instrument);
}

/// <summary>Renders the pitches produced by each scale section in an extended playback plan.</summary>
public interface IDynamicShawzinPreviewRenderer
{
    ShawzinPreviewAudio Render(DynamicShawzinScalePlan plan, ShawzinDefinition instrument);
}

/// <summary>Renders an original, dependency-free plucked-sine WAV preview; it uses no game audio assets.</summary>
public sealed class SyntheticShawzinPreviewRenderer : IShawzinPreviewRenderer
{
    public const int DefaultSampleRate = 22_050;
    internal const decimal NoteLengthSeconds = 0.35m;

    public ShawzinPreviewAudio Render(ShawzinTrack track, ShawzinDefinition instrument)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(instrument);
        if (!instrument.PlayProfile.Scales.TryGetValue(track.Scale, out var scale))
            throw new ArgumentException("The instrument does not support the track scale.", nameof(instrument));

        var inputToPitch = scale.Positions
            .GroupBy(value => value.Input)
            .ToDictionary(group => group.Key, group => group.First().Pitch);
        var duration = track.ShawzinEvents.Count == 0 ? 0m : track.ShawzinEvents[^1].Position.Seconds + NoteLengthSeconds;
        var samples = new double[checked((int)decimal.Ceiling(duration * DefaultSampleRate))];
        foreach (var shawzinEvent in track.ShawzinEvents)
        {
            var start = checked((int)decimal.Round(shawzinEvent.Position.Seconds * DefaultSampleRate, 0, MidpointRounding.AwayFromZero));
            foreach (var note in shawzinEvent.Chord.Notes)
            {
                if (!inputToPitch.TryGetValue(note, out var pitch)) continue;
                AddTone(samples, start, pitch);
            }
        }

        var peak = samples.Select(Math.Abs).DefaultIfEmpty(0d).Max();
        var gain = peak > 0.95d ? 0.95d / peak : 1d;
        var pcm = samples.Select(value => (short)Math.Round(Math.Clamp(value * gain, -1d, 1d) * short.MaxValue)).ToArray();
        return new ShawzinPreviewAudio(WriteWave(pcm), DefaultSampleRate, duration);
    }

    internal static void AddTone(double[] samples, int start, int pitch)
    {
        var count = Math.Min(samples.Length - start, (int)(DefaultSampleRate * NoteLengthSeconds));
        var frequency = 440d * Math.Pow(2d, (pitch - 69) / 12d);
        for (var index = 0; index < count; index++)
        {
            var time = index / (double)DefaultSampleRate;
            var envelope = Math.Exp(-8d * time);
            samples[start + index] += Math.Sin(2d * Math.PI * frequency * time) * envelope * 0.28d;
        }
    }

    internal static byte[] WriteWave(IReadOnlyList<short> samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataLength = checked(samples.Count * sizeof(short));
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(DefaultSampleRate);
        writer.Write(DefaultSampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataLength);
        foreach (var sample in samples) writer.Write(sample);
        return stream.ToArray();
    }
}

/// <summary>Dependency-free dynamic preview where every strike is decoded through its active scale.</summary>
public sealed class SyntheticDynamicShawzinPreviewRenderer : IDynamicShawzinPreviewRenderer
{
    public ShawzinPreviewAudio Render(DynamicShawzinScalePlan plan, ShawzinDefinition instrument)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(instrument);
        var duration = plan.NoteEvents.Count == 0 ? 0m : plan.NoteEvents.Max(value => value.Event.Position.Seconds) + SyntheticShawzinPreviewRenderer.NoteLengthSeconds;
        var samples = new double[checked((int)decimal.Ceiling(duration * SyntheticShawzinPreviewRenderer.DefaultSampleRate))];
        foreach (var dynamicEvent in plan.NoteEvents)
        {
            if (!instrument.Scales.TryGetValue(dynamicEvent.Scale, out var scale))
                throw new ArgumentException($"The instrument does not support dynamic scale '{dynamicEvent.Scale}'.", nameof(instrument));
            var pitches = scale.Positions.GroupBy(value => value.Input).ToDictionary(value => value.Key, value => value.First().Pitch);
            var start = checked((int)decimal.Round(dynamicEvent.Event.Position.Seconds * SyntheticShawzinPreviewRenderer.DefaultSampleRate,
                0, MidpointRounding.AwayFromZero));
            foreach (var input in dynamicEvent.Event.Chord.Notes)
                if (pitches.TryGetValue(input, out var pitch)) SyntheticShawzinPreviewRenderer.AddTone(samples, start, pitch);
        }
        var peak = samples.Select(Math.Abs).DefaultIfEmpty(0d).Max();
        var gain = peak > 0.95d ? 0.95d / peak : 1d;
        var pcm = samples.Select(value => (short)Math.Round(Math.Clamp(value * gain, -1d, 1d) * short.MaxValue)).ToArray();
        return new(SyntheticShawzinPreviewRenderer.WriteWave(pcm), SyntheticShawzinPreviewRenderer.DefaultSampleRate, duration);
    }
}
