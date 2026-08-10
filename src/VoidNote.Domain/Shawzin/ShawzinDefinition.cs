namespace VoidNote.Domain.Shawzin;

/// <summary>Describes optional behavior exposed by a Shawzin instrument.</summary>
[Flags]
public enum ShawzinCapabilities
{
    None = 0,
    SingleNotes = 1,
    Chords = 2,
    ChromaticScale = 4,
    Preview = 8,
}

/// <summary>Associates a musical pitch with one physical Shawzin input.</summary>
public sealed record ShawzinPitchPosition
{
    public ShawzinPitchPosition(int pitch, ShawzinNote input)
    {
        if (pitch is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(pitch));
        Pitch = pitch;
        Input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public int Pitch { get; }
    public ShawzinNote Input { get; }
}

/// <summary>Defines the pitches and physical input positions for one scale.</summary>
public sealed class ShawzinScaleDefinition
{
    private readonly IReadOnlyList<ShawzinPitchPosition> _positions;

    public ShawzinScaleDefinition(ShawzinScale scale, string displayName, IReadOnlyList<ShawzinPitchPosition> positions)
    {
        if (!Enum.IsDefined(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count == 0) throw new ArgumentException("A scale must expose at least one pitch.", nameof(positions));
        Scale = scale;
        DisplayName = displayName;
        _positions = positions.OrderBy(value => value.Pitch).ThenBy(value => value.Input.String).ThenBy(value => value.Input.Frets).ToArray();
    }

    public ShawzinScale Scale { get; }
    public string DisplayName { get; }
    public IReadOnlyList<ShawzinPitchPosition> Positions => _positions;
    public IReadOnlySet<int> PitchClasses => _positions.Select(value => value.Pitch % 12).ToHashSet();
}

/// <summary>Reusable physical play profile shared by differently sounding Shawzins.</summary>
public sealed class ShawzinPlayProfile
{
    private readonly IReadOnlyDictionary<ShawzinScale, ShawzinScaleDefinition> _scales;

    public ShawzinPlayProfile(string id, IReadOnlyList<ShawzinScaleDefinition> scales)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(scales);
        if (scales.Count == 0) throw new ArgumentException("A play profile must support at least one scale.", nameof(scales));
        if (scales.Select(value => value.Scale).Distinct().Count() != scales.Count)
            throw new ArgumentException("A play profile cannot define the same scale twice.", nameof(scales));
        Id = id;
        _scales = scales.ToDictionary(value => value.Scale);
    }

    public string Id { get; }
    public IReadOnlyDictionary<ShawzinScale, ShawzinScaleDefinition> Scales => _scales;
}

/// <summary>Separates preview timbre identity from physical playability.</summary>
public sealed record ShawzinSoundProfile
{
    public ShawzinSoundProfile(string id, string displayName, string previewPatch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewPatch);
        Id = id;
        DisplayName = displayName;
        PreviewPatch = previewPatch;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string PreviewPatch { get; }
}

/// <summary>Data-driven Shawzin instrument definition.</summary>
public sealed record ShawzinDefinition
{
    public ShawzinDefinition(string id, string displayName, ShawzinPlayProfile playProfile, ShawzinSoundProfile soundProfile, ShawzinCapabilities capabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        DisplayName = displayName;
        PlayProfile = playProfile ?? throw new ArgumentNullException(nameof(playProfile));
        SoundProfile = soundProfile ?? throw new ArgumentNullException(nameof(soundProfile));
        Capabilities = capabilities;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public ShawzinPlayProfile PlayProfile { get; }
    public ShawzinSoundProfile SoundProfile { get; }
    public ShawzinCapabilities Capabilities { get; }
    public IReadOnlyDictionary<ShawzinScale, ShawzinScaleDefinition> Scales => PlayProfile.Scales;
    public IReadOnlySet<int> AvailablePitches => PlayProfile.Scales.Values.SelectMany(value => value.Positions).Select(value => value.Pitch).ToHashSet();
}
