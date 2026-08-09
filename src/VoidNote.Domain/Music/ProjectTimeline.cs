namespace VoidNote.Domain.Music;

/// <summary>Defines the shared musical timeline and converts between ticks and absolute time.</summary>
public sealed class ProjectTimeline
{
    /// <summary>The default MIDI-compatible resolution.</summary>
    public const int DefaultTicksPerQuarterNote = 960;

    private readonly IReadOnlyList<TempoChange> _tempoChanges;

    /// <summary>Creates a timeline with a validated tempo map.</summary>
    public ProjectTimeline(int ticksPerQuarterNote, IReadOnlyList<TempoChange> tempoChanges)
    {
        if (ticksPerQuarterNote <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerQuarterNote));
        }

        ArgumentNullException.ThrowIfNull(tempoChanges);
        var orderedChanges = tempoChanges.OrderBy(change => change.Position.Ticks).ToArray();
        if (orderedChanges.Length == 0 || orderedChanges[0].Position != MusicalTime.Zero)
        {
            throw new ArgumentException("A tempo map must start at tick zero.", nameof(tempoChanges));
        }

        if (orderedChanges.Select(change => change.Position).Distinct().Count() != orderedChanges.Length)
        {
            throw new ArgumentException("A tempo map cannot contain duplicate positions.", nameof(tempoChanges));
        }

        TicksPerQuarterNote = ticksPerQuarterNote;
        _tempoChanges = orderedChanges;
    }

    /// <summary>Gets the number of ticks per quarter note.</summary>
    public int TicksPerQuarterNote { get; }

    /// <summary>Gets the ordered tempo changes.</summary>
    public IReadOnlyList<TempoChange> TempoChanges => _tempoChanges;

    /// <summary>Creates the default 120 BPM timeline.</summary>
    public static ProjectTimeline CreateDefault() =>
        new(DefaultTicksPerQuarterNote, [new TempoChange(MusicalTime.Zero, 120m)]);

    /// <summary>Converts a musical position into absolute seconds.</summary>
    public AbsoluteTime ToAbsoluteTime(MusicalTime position)
    {
        decimal seconds = 0m;
        long segmentStart = 0;
        decimal currentTempo = _tempoChanges[0].BeatsPerMinute;

        foreach (var change in _tempoChanges.Skip(1))
        {
            if (change.Position.Ticks > position.Ticks)
            {
                break;
            }

            seconds += TicksToSeconds(change.Position.Ticks - segmentStart, currentTempo);
            segmentStart = change.Position.Ticks;
            currentTempo = change.BeatsPerMinute;
        }

        seconds += TicksToSeconds(position.Ticks - segmentStart, currentTempo);
        return new AbsoluteTime(seconds);
    }

    /// <summary>Converts absolute seconds into the nearest musical tick.</summary>
    public MusicalTime ToMusicalTime(AbsoluteTime time)
    {
        decimal elapsedSeconds = 0m;
        long segmentStart = 0;
        decimal currentTempo = _tempoChanges[0].BeatsPerMinute;

        foreach (var change in _tempoChanges.Skip(1))
        {
            var segmentSeconds = TicksToSeconds(change.Position.Ticks - segmentStart, currentTempo);
            if (time.Seconds < elapsedSeconds + segmentSeconds)
            {
                return new MusicalTime(checked(segmentStart + SecondsToTicks(time.Seconds - elapsedSeconds, currentTempo)));
            }

            elapsedSeconds += segmentSeconds;
            segmentStart = change.Position.Ticks;
            currentTempo = change.BeatsPerMinute;
        }

        return new MusicalTime(checked(segmentStart + SecondsToTicks(time.Seconds - elapsedSeconds, currentTempo)));
    }

    private decimal TicksToSeconds(long ticks, decimal tempo) =>
        ticks * 60m / (tempo * TicksPerQuarterNote);

    private long SecondsToTicks(decimal seconds, decimal tempo) =>
        checked((long)decimal.Round(seconds * tempo * TicksPerQuarterNote / 60m, 0, MidpointRounding.AwayFromZero));
}
