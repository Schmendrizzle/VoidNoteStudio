using System.Text;
using VoidNote.Domain.Mandachord;

namespace VoidNote.Mandachord.Preview;

public sealed record MandachordPreviewResult(byte[] WaveData, TimeSpan Duration, int SampleRate, int EventCount);
public interface IMandachordPreviewRenderer { MandachordPreviewResult Render(MandachordPattern pattern, MandachordSoundSet soundSet, int sampleRate = 44_100); }

public sealed class SyntheticMandachordPreviewRenderer : IMandachordPreviewRenderer
{
    public MandachordPreviewResult Render(MandachordPattern pattern, MandachordSoundSet soundSet, int sampleRate = 44_100)
    {
        ArgumentNullException.ThrowIfNull(pattern); ArgumentNullException.ThrowIfNull(soundSet); pattern.Validate(); soundSet.Validate();
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        const decimal secondsPerStep = 0.125m; // documented fixed 120 BPM, sixteenth-note grid
        var frames = decimal.ToInt32(decimal.Ceiling(64m * secondsPerStep * sampleRate)); var samples = new double[frames];
        foreach (var step in pattern.Steps) RenderStep(samples, sampleRate, step, soundSet, decimal.ToDouble(step.StepIndex * secondsPerStep));
        var peak = samples.Select(Math.Abs).DefaultIfEmpty(1).Max(); var scale = peak > 0.95 ? 0.95 / peak : 1;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
        foreach (var value in samples) writer.Write((short)Math.Round(Math.Clamp(value * scale, -1, 1) * short.MaxValue, MidpointRounding.AwayFromZero));
        writer.Flush(); return new(stream.ToArray(), TimeSpan.FromSeconds(frames / (double)sampleRate), sampleRate, pattern.Steps.Count);
    }

    private static void RenderStep(double[] samples, int rate, MandachordStep step, MandachordSoundSet set, double start)
    {
        var patch = step.Layer switch { MandachordLayer.Bass => set.Bass, MandachordLayer.Melody => set.Melody, _ => set.Percussion };
        var duration = decimal.ToDouble(patch.ReleaseSeconds); var begin = (int)Math.Round(start * rate, MidpointRounding.AwayFromZero); var count = Math.Min(samples.Length - begin, (int)Math.Ceiling(duration * rate));
        var pitch = step.Layer switch { MandachordLayer.Bass => MandachordGridDefinition.Standard.BassPitches[step.PitchPosition!.Value].PreviewMidiPitch, MandachordLayer.Melody => MandachordGridDefinition.Standard.MelodyPitches[step.PitchPosition!.Value].PreviewMidiPitch, _ => 0 };
        var frequency = pitch == 0 ? 0 : 440d * Math.Pow(2, (pitch - 69) / 12d); var gain = decimal.ToDouble(patch.Gain) * step.Velocity / 127d;
        for (var i = 0; i < count; i++)
        {
            var time = i / (double)rate; var envelope = Math.Exp(-5d * time / Math.Max(duration, 0.01)); double signal;
            if (step.Layer == MandachordLayer.Percussion)
            {
                var hash = unchecked((uint)(step.StepIndex * 1103515245 + (int)step.PercussionCategory! * 12345 + i)); var noise = ((hash >> 8) & 0xffff) / 32768d - 1d;
                var tone = step.PercussionCategory switch { MandachordPercussionCategory.Kick => Math.Sin(2 * Math.PI * 70 * time), MandachordPercussionCategory.Snare => Math.Sin(2 * Math.PI * 180 * time), _ => 0d };
                signal = step.PercussionCategory == MandachordPercussionCategory.Kick ? 0.8 * tone + 0.2 * noise : 0.25 * tone + 0.75 * noise;
            }
            else signal = Math.Sin(2 * Math.PI * frequency * time) + decimal.ToDouble(patch.HarmonicMix) * Math.Sin(4 * Math.PI * frequency * time);
            samples[begin + i] += gain * envelope * signal;
        }
    }
}

public interface ICombinedPreviewRenderer { byte[] Mix(IReadOnlyList<byte[]> monoPcm16Waves); }
public sealed class PcmCombinedPreviewRenderer : ICombinedPreviewRenderer
{
    public byte[] Mix(IReadOnlyList<byte[]> waves)
    {
        if (waves.Count == 0) throw new ArgumentException("At least one preview is required.", nameof(waves));
        var sampleRate = BitConverter.ToInt32(waves[0], 24); var data = waves.Select(ReadSamples).ToArray();
        if (waves.Any(value => BitConverter.ToInt32(value, 24) != sampleRate)) throw new InvalidDataException("Combined previews must share a sample rate.");
        var length = data.Max(value => value.Length); var mixed = new short[length];
        for (var i = 0; i < length; i++) mixed[i] = (short)Math.Clamp(decimal.ToInt32(decimal.Round(data.Where(value => i < value.Length).Sum(value => value[i]) / (decimal)waves.Count, 0, MidpointRounding.AwayFromZero)), short.MinValue, short.MaxValue);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(length * 2); foreach (var value in mixed) writer.Write(value); writer.Flush(); return stream.ToArray();
    }
    private static short[] ReadSamples(byte[] wave)
    {
        if (wave.Length < 44 || Encoding.ASCII.GetString(wave, 0, 4) != "RIFF" || BitConverter.ToInt16(wave, 22) != 1 || BitConverter.ToInt16(wave, 34) != 16) throw new InvalidDataException("Only mono PCM16 WAV previews can be combined.");
        var result = new short[(wave.Length - 44) / 2]; Buffer.BlockCopy(wave, 44, result, 0, result.Length * 2); return result;
    }
}
