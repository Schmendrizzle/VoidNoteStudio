using System.Reflection;
using VoidNote.Audio.Decoding;
using VoidNote.Domain.Audio;

namespace VoidNote.Audio.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Domain_HasNoDecoderBackendOrAvaloniaDependency()
    {
        var references = typeof(AudioTrack).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();
        Assert.DoesNotContain(references, value => value!.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value!.Contains("FFmpeg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MidiShawzinAndGameBridge_DoNotReferenceAudioModule()
    {
        foreach (var name in new[] { "VoidNote.Midi", "VoidNote.Shawzin", "VoidNote.GameBridge" })
            Assert.DoesNotContain(Assembly.Load(name).GetReferencedAssemblies(), value => value.Name == typeof(IAudioDecoder).Assembly.GetName().Name);
    }

    [Fact]
    public void MilestoneG_DoesNotExposeTranscriptionOrSeparationEngines()
    {
        var names = typeof(IAudioDecoder).Assembly.ExportedTypes.Select(value => value.Name).ToArray();
        Assert.DoesNotContain(names, value => value.Contains("Transcription", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, value => value.Contains("Separation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, value => value.Contains("PitchDetection", StringComparison.OrdinalIgnoreCase));
    }
}
