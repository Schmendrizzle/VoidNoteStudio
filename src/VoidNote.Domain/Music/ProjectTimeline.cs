namespace VoidNote.Domain.Music;

/// <summary>Defines the shared musical timeline and converts between ticks and absolute time.</summary>
public sealed class ProjectTimeline
{
    /// <summary>The default MIDI-compatible resolution.</summary>
    public const int DefaultTicksPerQuarterNote = 960;

    private readonly IReadOnlyList<TempoChange> _tempoChanges;
    private readonly IReadOnlyList<TimeSignatureChange> _timeSignatureChanges;

    /// <summary>Creates a timeline with a validated tempo map.</summary>
    public ProjectTimeline(
        int ticksPerQuarterNote,
        IReadOnlyList<TempoChange> tempoChanges,
        IReadOnlyList<TimeSignatureChange>? timeSignatureChanges = null)
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

        var orderedSignatures = (timeSignatureChanges ??
            [new TimeSignatureChange(MusicalTime.Zero, 4, 4)])
            .OrderBy(change => change.Position.Ticks)
            .ToArray();
        if (orderedSignatures.Length == 0 || orderedSignatures[0].Position != MusicalTime.Zero)
        {
            throw new ArgumentException("A time-signature map must start at tick zero.", nameof(timeSignatureChanges));
        }

        if (orderedSignatures.Select(change => change.Position).Distinct().Count() != orderedSignatures.Length)
        {
            throw new ArgumentException("A time-signature map cannot contain duplicate positions.", nameof(timeSignatureChanges));
        }

        TicksPerQuarterNote = ticksPerQuarterNote;
        _tempoChanges = orderedChanges;
        _timeSignatureChanges = orderedSignatures;
    }

    /// <summary>Gets the number of ticks per quarter note.</summary>
    public int TicksPerQuarterNote { get; }

    /// <summary>Gets the ordered tempo changes.</summary>
    public IReadOnlyList<TempoChange> TempoChanges => _tempoChanges;

    /// <summary>Gets the ordered time-signature changes.</summary>
    public IReadOnlyList<TimeSignatureChange> TimeSignatureChanges => _timeSignatureChanges;

    /// <summary>Creates the default 120 BPM timeline.</summary>
    public static ProjectTimeline CreateDefault() =>
        new(DefaultTicksPerQuarterNote, [new TempoChange(MusicalTime.Zero, 120m)]);

    /// <summary>Converts ticks to quarter-note beats without rounding.</summary>
    public decimal ToBeats(MusicalTime position) =>
        position.Ticks / (decimal)TicksPerQuarterNote;

    /// <summary>Converts quarter-note beats to the nearest master tick.</summary>
    public MusicalTime FromBeats(decimal beats)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(beats);
        return new MusicalTime(checked((long)decimal.Round(
            beats * TicksPerQuarterNote,
            0,
            MidpointRounding.AwayFromZero)));
    }

    /// <summary>Converts ticks to a zero-based fractional bar coordinate.</summary>
    public decimal ToBars(MusicalTime position)
    {
        var musicalPosition = ToMusicalPosition(position);
        var signature = GetTimeSignatureAt(position);
        var ticksPerBeat = TicksPerBeat(signature);
        var ticksIntoBar = checked((musicalPosition.Beat - 1L) * ticksPerBeat + musicalPosition.TickInBeat);
        return musicalPosition.Bar - 1m + ticksIntoBar / (decimal)(ticksPerBeat * signature.Numerator);
    }

    /// <summary>Converts a zero-based fractional bar coordinate to the nearest master tick.</summary>
    public MusicalTime FromBars(decimal bars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bars);
        var wholeBars = decimal.ToInt64(decimal.Floor(bars));
        var fraction = bars - wholeBars;
        var bar = checked(wholeBars + 1);
        var barStart = FromMusicalPosition(new MusicalPosition(bar, 1, 0));
        var signature = GetTimeSignatureAt(barStart);
        var ticksPerBeat = TicksPerBeat(signature);
        var ticksIntoBar = checked((long)decimal.Round(
            fraction * ticksPerBeat * signature.Numerator,
            0,
            MidpointRounding.AwayFromZero));
        if (ticksIntoBar == ticksPerBeat * signature.Numerator)
        {
            return FromMusicalPosition(new MusicalPosition(checked(bar + 1), 1, 0));
        }

        return FromMusicalPosition(new MusicalPosition(
            bar,
            checked((int)(ticksIntoBar / ticksPerBeat) + 1),
            ticksIntoBar % ticksPerBeat));
    }

    /// <summary>Converts a tick position into a bar/beat/tick position using the time-signature map.</summary>
    public MusicalPosition ToMusicalPosition(MusicalTime position)
    {
        long barOffset = 0;
        long segmentStart = 0;
        var signature = _timeSignatureChanges[0];

        foreach (var change in _timeSignatureChanges.Skip(1))
        {
            if (change.Position.Ticks > position.Ticks)
            {
                break;
            }

            barOffset += CountBars(change.Position.Ticks - segmentStart, signature);
            segmentStart = change.Position.Ticks;
            signature = change;
        }

        var ticksPerBeat = TicksPerBeat(signature);
        var ticksPerBar = checked(ticksPerBeat * signature.Numerator);
        var localTicks = position.Ticks - segmentStart;
        return new MusicalPosition(
            checked(barOffset + localTicks / ticksPerBar + 1),
            checked((int)(localTicks % ticksPerBar / ticksPerBeat) + 1),
            localTicks % ticksPerBeat);
    }

    /// <summary>Converts a bar/beat/tick position into a master-timeline tick.</summary>
    public MusicalTime FromMusicalPosition(MusicalPosition position)
    {
        long barOffset = 0;
        long segmentStart = 0;

        for (var index = 0; index < _timeSignatureChanges.Count; index++)
        {
            var signature = _timeSignatureChanges[index];
            var nextStart = index + 1 < _timeSignatureChanges.Count
                ? _timeSignatureChanges[index + 1].Position.Ticks
                : long.MaxValue;
            var segmentBars = nextStart == long.MaxValue
                ? long.MaxValue
                : CountBars(nextStart - segmentStart, signature);
            var localBar = position.Bar - barOffset - 1;

            if (localBar < segmentBars)
            {
                if (position.Beat > signature.Numerator)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), "The beat exceeds the active time signature.");
                }

                var ticksPerBeat = TicksPerBeat(signature);
                if (position.TickInBeat >= ticksPerBeat)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), "The tick offset exceeds the active beat.");
                }

                var ticks = checked(
                    segmentStart +
                    localBar * ticksPerBeat * signature.Numerator +
                    (position.Beat - 1L) * ticksPerBeat +
                    position.TickInBeat);
                if (ticks >= nextStart)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), "The position lies beyond a partial bar ending at a time-signature change.");
                }

                return new MusicalTime(ticks);
            }

            barOffset = checked(barOffset + segmentBars);
            segmentStart = nextStart;
        }

        throw new ArgumentOutOfRangeException(nameof(position));
    }

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

    private long CountBars(long ticks, TimeSignatureChange signature)
    {
        var ticksPerBar = checked(TicksPerBeat(signature) * signature.Numerator);
        return checked(ticks / ticksPerBar + (ticks % ticksPerBar == 0 ? 0 : 1));
    }

    private long TicksPerBeat(TimeSignatureChange signature)
    {
        var numerator = checked((long)TicksPerQuarterNote * 4);
        if (numerator % signature.Denominator != 0)
        {
            throw new InvalidOperationException("The timeline resolution cannot represent this time signature exactly.");
        }

        return numerator / signature.Denominator;
    }

    private TimeSignatureChange GetTimeSignatureAt(MusicalTime position) =>
        _timeSignatureChanges.Last(change => change.Position.Ticks <= position.Ticks);
}
