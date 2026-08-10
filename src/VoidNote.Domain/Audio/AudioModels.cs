using System.Text.Json.Serialization;
using VoidNote.Domain.Music;
using VoidNote.Domain.Projects;

namespace VoidNote.Domain.Audio;

/// <summary>Describes one decoded audio channel without depending on a decoder library.</summary>
public sealed record AudioChannelInfo(int Index, string Name);

/// <summary>Contains format-independent metadata discovered while probing audio.</summary>
public sealed record AudioFormatInfo
{
    public required string Container { get; init; }
    public required string Codec { get; init; }
    public required int SampleRate { get; init; }
    public required int ChannelCount { get; init; }
    public int? BitDepth { get; init; }
    public long? BitRate { get; init; }
    public required AbsoluteTime Duration { get; init; }
    public IReadOnlyList<AudioChannelInfo> Channels { get; init; } = [];
    public string? Title { get; init; }
    public string? Artist { get; init; }
}

/// <summary>Places a non-destructive window of a source on the master timeline.</summary>
public sealed class AudioClip : ProjectItem
{
    public required Guid SourceId { get; init; }
    public MusicalTime Start { get; set; } = MusicalTime.Zero;
    public AbsoluteTime TrimIn { get; set; } = AbsoluteTime.Zero;
    public required AbsoluteTime Duration { get; set; }
    public decimal Gain { get; set; } = 1m;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Represents a project audio lane containing clips backed by immutable sources.</summary>
public sealed class AudioTrack : ProjectItem
{
    public List<AudioClip> Clips { get; init; } = [];
    public decimal Gain { get; set; } = 1m;
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Represents a timeline selection and optional loop without altering source media.</summary>
public sealed class AudioRegion : ProjectItem
{
    public AbsoluteTime Start { get; set; } = AbsoluteTime.Zero;
    public AbsoluteTime End { get; set; } = AbsoluteTime.Zero;
    public bool LoopEnabled { get; set; }
    [JsonIgnore]
    public AbsoluteTime Duration => new(End.Seconds - Start.Seconds);

    public void Validate()
    {
        if (End.Seconds < Start.Seconds)
            throw new InvalidOperationException("An audio region cannot end before it starts.");
    }
}
