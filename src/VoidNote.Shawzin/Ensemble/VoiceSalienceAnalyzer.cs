using VoidNote.Domain.Music;

namespace VoidNote.Shawzin.Ensemble;

/// <summary>Scores melody and bass salience without external or learned dependencies.</summary>
public sealed class VoiceSalienceAnalyzer
{
    public decimal MelodyScore(MusicalEvent note, IReadOnlyList<MusicalEvent> localNotes, MusicalEvent? previousMelody)
    {
        var pitch = Relative(note.Pitch, localNotes.Min(value => value.Pitch), localNotes.Max(value => value.Pitch));
        var velocity = note.Velocity / 127m;
        var duration = Math.Min(1m, note.Duration.Ticks / 1920m);
        var continuity = previousMelody is null ? 0.5m : 1m - Math.Min(1m, Math.Abs(note.Pitch - previousMelody.Pitch) / 24m);
        var density = 1m / localNotes.Count;
        return Round(0.34m * pitch + 0.22m * velocity + 0.16m * duration + 0.20m * continuity + 0.08m * density);
    }

    public decimal BassScore(MusicalEvent note, IReadOnlyList<MusicalEvent> localNotes, MusicalEvent? previousBass)
    {
        var register = 1m - Relative(note.Pitch, localNotes.Min(value => value.Pitch), localNotes.Max(value => value.Pitch));
        var duration = Math.Min(1m, note.Duration.Ticks / 1920m);
        var continuity = previousBass is null ? 0.5m : 1m - Math.Min(1m, Math.Abs(note.Pitch - previousBass.Pitch) / 18m);
        var rhythm = previousBass is null ? 0.5m : 1m - Math.Min(1m, Math.Abs(note.StartTime.Ticks - previousBass.StartTime.Ticks - previousBass.Duration.Ticks) / 1920m);
        return Round(0.42m * register + 0.22m * duration + 0.22m * continuity + 0.14m * rhythm);
    }

    private static decimal Relative(int value, int minimum, int maximum) => maximum == minimum ? 0.5m : (value - minimum) / (decimal)(maximum - minimum);
    private static decimal Round(decimal value) => decimal.Round(Math.Clamp(value, 0m, 1m), 4, MidpointRounding.AwayFromZero);
}
