using VoidNote.Domain.Music;
using VoidNote.Domain.Shawzin;
using VoidNote.Shawzin.Definitions;
using VoidNote.Shawzin.Preview;

namespace VoidNote.Shawzin.Tests;

public sealed class ShawzinPreviewTests
{
    [Fact]
    public void SyntheticPreview_ProducesDeterministicLegalPcmWave()
    {
        var track = new ShawzinTrack
        {
            Scale = ShawzinScale.Chromatic,
            ShawzinEvents = [new ShawzinEvent(Guid.NewGuid(), AbsoluteTime.Zero, new ShawzinChord([new ShawzinNote(ShawzinString.First, ShawzinFret.None)]))],
        };
        var renderer = new SyntheticShawzinPreviewRenderer();

        var first = renderer.Render(track, BuiltInShawzinDefinitions.Default);
        var second = renderer.Render(track, BuiltInShawzinDefinitions.Default);

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(first.WaveData, 0, 4));
        Assert.Equal(first.WaveData, second.WaveData);
        Assert.True(first.WaveData.Length > 44);
    }
}
