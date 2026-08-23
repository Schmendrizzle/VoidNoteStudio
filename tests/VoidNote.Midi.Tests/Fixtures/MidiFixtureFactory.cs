using System.Text;

namespace VoidNote.Midi.Tests.Fixtures;

internal static class MidiFixtureFactory
{
    public const ushort Ppq = 480;

    public static MemoryStream SingleNote() => Create(
        new FixtureTrack("Lead", [new FixtureNote(120, 360, 60, 91)]));

    public static MemoryStream SequentialNotes() => Create(
        new FixtureTrack("Sequence",
        [
            new FixtureNote(0, 240, 60, 70),
            new FixtureNote(240, 240, 62, 80),
            new FixtureNote(480, 480, 64, 90),
        ]));

    public static MemoryStream ChromaticValidation() => Create(
        new FixtureTrack("Chromatic validation", Enumerable.Range(0, 12)
            .Select(index => new FixtureNote(index * 120, 120, checked((byte)(60 + index)), 100)).ToArray()));

    public static MemoryStream SyntheticKnownMotif() => Create(
        new FixtureTrack("Synthetic test motif", new byte[] { 60, 64, 67, 72, 71, 67, 64, 60 }
            .Select((pitch, index) => new FixtureNote(index * 120, 120, pitch, 96)).ToArray()));

    public static MemoryStream Chord() => Create(
        new FixtureTrack("Chord",
        [
            new FixtureNote(0, 480, 60, 100),
            new FixtureNote(0, 480, 64, 100),
            new FixtureNote(0, 480, 67, 100),
        ]));

    public static MemoryStream PolyphonicCreatorFlow() => Create(
        new FixtureTrack("Piano",
        [
            new FixtureNote(0, 480, 48, 90), new FixtureNote(0, 480, 55, 95), new FixtureNote(0, 480, 62, 110), new FixtureNote(0, 480, 67, 88),
            new FixtureNote(480, 480, 50, 90), new FixtureNote(480, 480, 57, 95), new FixtureNote(480, 480, 64, 110), new FixtureNote(480, 480, 69, 88),
            new FixtureNote(960, 480, 52, 90), new FixtureNote(960, 480, 59, 95), new FixtureNote(960, 480, 65, 110), new FixtureNote(960, 480, 71, 88),
        ]));

    public static MemoryStream MultipleTracks() => Create(
        new FixtureTrack("Lead", [new FixtureNote(0, 480, 72, 110)]),
        new FixtureTrack("Bass", [new FixtureNote(240, 960, 36, 76)]));

    public static MemoryStream Velocities() => Create(
        new FixtureTrack("Dynamics",
        [
            new FixtureNote(0, 120, 60, 1),
            new FixtureNote(120, 120, 61, 64),
            new FixtureNote(240, 120, 62, 127),
        ]));

    public static MemoryStream TempoChange() => Create(
        new FixtureTrack(
            "Tempo",
            [new FixtureNote(0, 1_920, 60, 100)],
            [new FixtureTempo(0, 500_000), new FixtureTempo(960, 1_000_000)]));

    public static MemoryStream TimeSignatures() => Create(
        new FixtureTrack(
            "Meter",
            [new FixtureNote(0, 3_840, 60, 100)],
            TimeSignatureValues:
            [
                new FixtureTimeSignature(0, 4, 4),
                new FixtureTimeSignature(1_920, 3, 4),
            ]));

    public static MemoryStream LongTiming() => Create(
        new FixtureTrack(
            "Long",
            [new FixtureNote(1_000_000, 123_456, 64, 87)],
            [new FixtureTempo(0, 486_003)]));

    public static MemoryStream ComplexRoundTrip() => Create(
        new FixtureTrack(
            "Lead",
            [
                new FixtureNote(0, 240, 60, 32),
                new FixtureNote(480, 480, 64, 96),
                new FixtureNote(480, 960, 67, 127),
            ],
            [new FixtureTempo(0, 500_000), new FixtureTempo(960, 750_000)],
            [new FixtureTimeSignature(0, 4, 4), new FixtureTimeSignature(1_920, 3, 4)]),
        new FixtureTrack("Bass", [new FixtureNote(240, 1_440, 36, 73)]));

    private static MemoryStream Create(params FixtureTrack[] tracks)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write("MThd"u8);
        WriteUInt32(writer, 6);
        WriteUInt16(writer, tracks.Length > 1 ? (ushort)1 : (ushort)0);
        WriteUInt16(writer, checked((ushort)tracks.Length));
        WriteUInt16(writer, Ppq);

        foreach (var track in tracks)
        {
            var data = BuildTrack(track);
            writer.Write("MTrk"u8);
            WriteUInt32(writer, checked((uint)data.Length));
            writer.Write(data);
        }

        stream.Position = 0;
        return stream;
    }

    private static byte[] BuildTrack(FixtureTrack track)
    {
        var nameBytes = Encoding.UTF8.GetBytes(track.Name);
        var events = new List<RawEvent>
        {
            new(0, 0, [0xFF, 0x03, .. EncodeVariableLength(nameBytes.Length), .. nameBytes]),
        };

        events.AddRange(track.Tempos.Select(tempo => new RawEvent(
            tempo.Tick,
            0,
            [
                0xFF, 0x51, 0x03,
                (byte)(tempo.MicrosecondsPerQuarter >> 16),
                (byte)((tempo.MicrosecondsPerQuarter >> 8) & 0xFF),
                (byte)(tempo.MicrosecondsPerQuarter & 0xFF),
            ])));
        events.AddRange(track.TimeSignatures.Select(signature => new RawEvent(
            signature.Tick,
            0,
            [0xFF, 0x58, 0x04, signature.Numerator, DenominatorExponent(signature.Denominator), 24, 8])));

        foreach (var note in track.Notes)
        {
            events.Add(new RawEvent(note.Start + note.Duration, 1, [0x80, note.Pitch, 0]));
            events.Add(new RawEvent(note.Start, 2, [0x90, note.Pitch, note.Velocity]));
        }

        using var data = new MemoryStream();
        long previous = 0;
        foreach (var midiEvent in events.OrderBy(item => item.Tick).ThenBy(item => item.SortOrder))
        {
            data.Write(EncodeVariableLength(midiEvent.Tick - previous));
            data.Write(midiEvent.Data);
            previous = midiEvent.Tick;
        }

        data.WriteByte(0);
        data.Write([0xFF, 0x2F, 0x00]);
        return data.ToArray();
    }

    private static byte[] EncodeVariableLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Span<byte> reversed = stackalloc byte[10];
        var count = 0;
        reversed[count++] = checked((byte)(value & 0x7F));
        while ((value >>= 7) > 0)
        {
            reversed[count++] = checked((byte)((value & 0x7F) | 0x80));
        }

        var result = new byte[count];
        for (var index = 0; index < count; index++) result[index] = reversed[count - index - 1];
        return result;
    }

    private static byte DenominatorExponent(byte denominator)
    {
        byte exponent = 0;
        while (denominator > 1)
        {
            denominator /= 2;
            exponent++;
        }

        return exponent;
    }

    private static void WriteUInt16(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteUInt32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private sealed record RawEvent(long Tick, int SortOrder, byte[] Data);
    private sealed record FixtureTrack(
        string Name,
        IReadOnlyList<FixtureNote> Notes,
        IReadOnlyList<FixtureTempo>? TempoValues = null,
        IReadOnlyList<FixtureTimeSignature>? TimeSignatureValues = null)
    {
        public IReadOnlyList<FixtureTempo> Tempos { get; } = TempoValues ?? [];
        public IReadOnlyList<FixtureTimeSignature> TimeSignatures { get; } = TimeSignatureValues ?? [];
    }

    private sealed record FixtureNote(long Start, long Duration, byte Pitch, byte Velocity);
    private sealed record FixtureTempo(long Tick, int MicrosecondsPerQuarter);
    private sealed record FixtureTimeSignature(long Tick, byte Numerator, byte Denominator);
}
