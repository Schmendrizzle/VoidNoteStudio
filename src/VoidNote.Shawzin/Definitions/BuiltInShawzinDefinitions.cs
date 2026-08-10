using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Definitions;

/// <summary>Provides immutable built-in instrument data without embedding mapping decisions in algorithms.</summary>
public static class BuiltInShawzinDefinitions
{
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
        "standard-24-position",
        [
            Scale(ShawzinScale.PentatonicMinor, "Pentatonic Minor", [0, 3, 5, 7, 10]),
            Scale(ShawzinScale.PentatonicMajor, "Pentatonic Major", [0, 2, 4, 7, 9]),
            Scale(ShawzinScale.Chromatic, "Chromatic", Enumerable.Range(0, 12).ToArray()),
            Scale(ShawzinScale.Hexatonic, "Hexatonic", [0, 2, 4, 6, 8, 10]),
            Scale(ShawzinScale.Major, "Major", [0, 2, 4, 5, 7, 9, 11]),
            Scale(ShawzinScale.Minor, "Minor", [0, 2, 3, 5, 7, 8, 10]),
            Scale(ShawzinScale.Hirajoshi, "Hirajoshi", [0, 2, 3, 7, 8]),
            Scale(ShawzinScale.Phrygian, "Phrygian", [0, 1, 3, 5, 7, 8, 10]),
            Scale(ShawzinScale.Yo, "Yo", [0, 2, 5, 7, 9]),
        ]);

    private static ShawzinScaleDefinition Scale(ShawzinScale scale, string name, IReadOnlyList<int> pitchClasses)
    {
        const int rootPitch = 48;
        int[] stringDegreeOffsets = [0, 7, 14];
        var positions = new List<ShawzinPitchPosition>(24);
        for (var stringIndex = 0; stringIndex < 3; stringIndex++)
        {
            for (var fretMask = 0; fretMask < 8; fretMask++)
            {
                var degree = stringDegreeOffsets[stringIndex] + fretMask;
                var octave = degree / pitchClasses.Count;
                var pitch = rootPitch + octave * 12 + pitchClasses[degree % pitchClasses.Count];
                positions.Add(new ShawzinPitchPosition(
                    pitch,
                    new ShawzinNote((ShawzinString)(stringIndex + 1), (ShawzinFret)fretMask)));
            }
        }

        return new ShawzinScaleDefinition(scale, name, positions);
    }
}
