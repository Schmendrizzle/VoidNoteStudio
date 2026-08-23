using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Definitions;

/// <summary>Provides immutable built-in instrument data without embedding mapping decisions in algorithms.</summary>
public static class BuiltInShawzinDefinitions
{
    private static readonly (ShawzinString String, ShawzinFret Fret, char Symbol)[] PhysicalPositions =
    [
        (ShawzinString.First, ShawzinFret.None, 'B'),
        (ShawzinString.Second, ShawzinFret.None, 'C'),
        (ShawzinString.Third, ShawzinFret.None, 'E'),
        (ShawzinString.First, ShawzinFret.Sky, 'J'),
        (ShawzinString.Second, ShawzinFret.Sky, 'K'),
        (ShawzinString.Third, ShawzinFret.Sky, 'M'),
        (ShawzinString.First, ShawzinFret.Earth, 'R'),
        (ShawzinString.Second, ShawzinFret.Earth, 'S'),
        (ShawzinString.Third, ShawzinFret.Earth, 'U'),
        (ShawzinString.First, ShawzinFret.Water, 'h'),
        (ShawzinString.Second, ShawzinFret.Water, 'i'),
        (ShawzinString.Third, ShawzinFret.Water, 'k'),
    ];

    private static readonly ShawzinPlayProfile StandardProfile = CreateStandardProfile();
    private static readonly IReadOnlyList<ShawzinDefinition> Definitions =
    [
        new(
            "dax",
            "Dax Shawzin",
            StandardProfile,
            new ShawzinSoundProfile("dax-clean", "Dax", "SyntheticPluckedString"),
            ShawzinCapabilities.SingleNotes | ShawzinCapabilities.Chords | ShawzinCapabilities.ChromaticScale | ShawzinCapabilities.Preview),
        new(
            "nelumbo",
            "Nelumbo Shawzin",
            StandardProfile,
            new ShawzinSoundProfile("nelumbo-warm", "Nelumbo", "SyntheticWarmPluck"),
            ShawzinCapabilities.SingleNotes | ShawzinCapabilities.Chords | ShawzinCapabilities.ChromaticScale | ShawzinCapabilities.Preview),
    ];

    public static IReadOnlyList<ShawzinDefinition> All => Definitions;

    public static ShawzinDefinition Default => Definitions[0];

    public static ShawzinDefinition Get(string id) =>
        Definitions.FirstOrDefault(value => string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Unknown Shawzin definition '{id}'.");

    private static ShawzinPlayProfile CreateStandardProfile() => new(
        "warframe-standard-12-position-v1",
        [
            Scale(ShawzinScale.PentatonicMinor, "Pentatonic Minor", [60, 63, 65, 67, 70, 72, 75, 77, 79, 82, 84, 87]),
            Scale(ShawzinScale.PentatonicMajor, "Pentatonic Major", [60, 62, 64, 67, 69, 72, 74, 76, 79, 81, 84, 86]),
            Scale(ShawzinScale.Chromatic, "Chromatic", [60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71]),
            Scale(ShawzinScale.Hexatonic, "Hexatonic", [60, 63, 65, 66, 67, 70, 72, 75, 77, 78, 79, 82]),
            Scale(ShawzinScale.Major, "Major", [60, 62, 64, 65, 67, 69, 71, 72, 74, 76, 77, 79]),
            Scale(ShawzinScale.Minor, "Minor", [60, 62, 63, 65, 67, 68, 70, 72, 74, 75, 77, 79]),
            Scale(ShawzinScale.Hirajoshi, "Hirajoshi", [60, 61, 65, 66, 70, 72, 73, 77, 78, 82, 84, 85]),
            Scale(ShawzinScale.Phrygian, "Phrygian Dominant", [60, 61, 64, 65, 67, 68, 70, 72, 73, 76, 77, 79]),
            Scale(ShawzinScale.Yo, "Yo", [61, 63, 66, 68, 70, 73, 75, 78, 80, 82, 85, 87]),
        ]);

    private static ShawzinScaleDefinition Scale(ShawzinScale scale, string name, IReadOnlyList<int> pitches)
    {
        if (pitches.Count != PhysicalPositions.Length) throw new ArgumentException("A real Warframe scale must define exactly twelve positions.", nameof(pitches));
        var positions = PhysicalPositions.Select((physical, index) => new ShawzinPitchPosition(
            index, pitches[index], new ShawzinNote(physical.String, physical.Fret), physical.Symbol)).ToArray();
        return new ShawzinScaleDefinition(scale, name, positions);
    }
}
