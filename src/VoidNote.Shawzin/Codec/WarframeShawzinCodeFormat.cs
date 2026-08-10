using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Codec;

/// <summary>Defines the isolated Warframe recorded-song V1 wire representation.</summary>
public static class WarframeShawzinCodeFormat
{
    /// <summary>The standard Base64 alphabet used as a positional digit alphabet.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>The number of characters occupied by one event.</summary>
    public const int EventWidth = 3;

    /// <summary>The maximum 12-bit timestamp value.</summary>
    public const int MaximumTimestamp = 4095;

    /// <summary>The maximum event count implied by unique 12-bit timestamps.</summary>
    public const int MaximumEventCount = MaximumTimestamp + 1;

    /// <summary>The format timing quantum in seconds.</summary>
    public const decimal SecondsPerTimestamp = 0.0625m;

    internal static bool TryGetValue(char symbol, out int value)
    {
        value = Alphabet.IndexOf(symbol, StringComparison.Ordinal);
        return value >= 0;
    }

    internal static char GetSymbol(int value) => Alphabet[value];

    internal static ShawzinChord DecodeChord(int value)
    {
        var stringMask = value & 0b111;
        var frets = (ShawzinFret)(value >> 3);
        var notes = new List<ShawzinNote>(3);
        if ((stringMask & 0b001) != 0) notes.Add(new ShawzinNote(ShawzinString.First, frets));
        if ((stringMask & 0b010) != 0) notes.Add(new ShawzinNote(ShawzinString.Second, frets));
        if ((stringMask & 0b100) != 0) notes.Add(new ShawzinNote(ShawzinString.Third, frets));
        return new ShawzinChord(notes);
    }

    internal static int EncodeChord(ShawzinChord chord)
    {
        var stringMask = chord.Notes.Aggregate(0, (mask, note) => mask | (note.String switch
        {
            ShawzinString.First => 0b001,
            ShawzinString.Second => 0b010,
            ShawzinString.Third => 0b100,
            _ => 0,
        }));
        return ((int)chord.Frets << 3) | stringMask;
    }

    internal static int QuantizeTimestamp(decimal seconds) => checked((int)decimal.Round(
        seconds / SecondsPerTimestamp,
        0,
        MidpointRounding.AwayFromZero));
}
