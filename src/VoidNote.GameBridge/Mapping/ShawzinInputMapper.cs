using VoidNote.Domain.Shawzin;
using VoidNote.GameBridge.Abstractions;
using VoidNote.GameBridge.Profiles;

namespace VoidNote.GameBridge.Mapping;

public sealed record ShawzinInputAction(Guid EventId, IReadOnlyList<GameInputKey> FretKeys, IReadOnlyList<GameInputKey> StringKeys)
{
    public IReadOnlyList<GameInputKey> AllKeys => [.. FretKeys, .. StringKeys];
}

public interface IShawzinInputMapper { ShawzinInputAction Map(ShawzinEvent value, ShawzinKeybindProfile profile); }

/// <summary>Maps physical Shawzin values to portable keys, independent of codec, UI and OS.</summary>
public sealed class ShawzinInputMapper : IShawzinInputMapper
{
    public ShawzinInputAction Map(ShawzinEvent value, ShawzinKeybindProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value); ArgumentNullException.ThrowIfNull(profile);
        var frets = new List<GameInputKey>();
        if (value.Chord.Frets.HasFlag(ShawzinFret.Sky)) frets.Add(profile.Get(ShawzinInputBinding.FretLeft));
        if (value.Chord.Frets.HasFlag(ShawzinFret.Earth)) frets.Add(profile.Get(ShawzinInputBinding.FretMiddle));
        if (value.Chord.Frets.HasFlag(ShawzinFret.Water)) frets.Add(profile.Get(ShawzinInputBinding.FretRight));
        if (frets.Count == 0 && profile.Bindings.ContainsKey(ShawzinInputBinding.Neutral)) frets.Add(profile.Get(ShawzinInputBinding.Neutral));
        var strings = value.Chord.Notes.Select(note => profile.Get(note.String switch
        {
            ShawzinString.First => ShawzinInputBinding.String1,
            ShawzinString.Second => ShawzinInputBinding.String2,
            ShawzinString.Third => ShawzinInputBinding.String3,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        })).ToArray();
        return new(value.Id, frets, strings);
    }
}
