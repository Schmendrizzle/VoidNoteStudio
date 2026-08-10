using System.Text.Json.Serialization;

namespace VoidNote.Domain.Music;

/// <summary>Represents an absolute timeline value as decimal seconds.</summary>
public readonly record struct AbsoluteTime
{
    /// <summary>Creates an absolute time value.</summary>
    [JsonConstructor]
    public AbsoluteTime(decimal seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        Seconds = seconds;
    }

    /// <summary>Gets the elapsed seconds without millisecond rounding.</summary>
    public decimal Seconds { get; }

    /// <summary>Gets the zero time.</summary>
    public static AbsoluteTime Zero => new(0m);
}
