using VoidNote.Domain.Shawzin;

namespace VoidNote.Shawzin.Dynamic;

/// <summary>The forward-only Scale Select cycle used by Warframe Shawzin playback.</summary>
public static class WarframeShawzinScaleCycle
{
    public static IReadOnlyList<ShawzinScale> Scales { get; } =
    [
        ShawzinScale.PentatonicMinor,
        ShawzinScale.PentatonicMajor,
        ShawzinScale.Chromatic,
        ShawzinScale.Hexatonic,
        ShawzinScale.Major,
        ShawzinScale.Minor,
        ShawzinScale.Hirajoshi,
        ShawzinScale.Phrygian,
        ShawzinScale.Yo,
    ];

    public static int RequiredForwardPresses(ShawzinScale current, ShawzinScale target)
    {
        var currentIndex = IndexOf(current, nameof(current));
        var targetIndex = IndexOf(target, nameof(target));
        return (targetIndex - currentIndex + Scales.Count) % Scales.Count;
    }

    private static int IndexOf(ShawzinScale scale, string parameterName)
    {
        for (var index = 0; index < Scales.Count; index++)
            if (Scales[index] == scale) return index;
        throw new ArgumentOutOfRangeException(parameterName, scale, "Scale is not in the Warframe cycle.");
    }
}
