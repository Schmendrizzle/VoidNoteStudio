namespace VoidNote.Domain.Shawzin;

/// <summary>Represents the scale and single event track carried by one Shawzin song code.</summary>
public sealed record ShawzinSong
{
    /// <summary>Creates a Shawzin song.</summary>
    public ShawzinSong(ShawzinTrack track)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
    }

    /// <summary>Gets the scale selected by the code header.</summary>
    public ShawzinScale Scale => Track.Scale;

    /// <summary>Gets the single track represented by the code.</summary>
    public ShawzinTrack Track { get; }
}
