using VoidNote.GameBridge.Abstractions;

namespace VoidNote.GameBridge.Profiles;

public enum ShawzinInputBinding { String1, String2, String3, FretLeft, FretMiddle, FretRight, Neutral }

/// <summary>A named, persistable set of user-owned Shawzin keyboard bindings.</summary>
public sealed record ShawzinKeybindProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public bool IsDefaultWarframeLayout { get; init; }
    public IReadOnlyDictionary<ShawzinInputBinding, string> Bindings { get; init; } = new Dictionary<ShawzinInputBinding, string>();

    public ShawzinKeybindProfile Duplicate(string name) => this with { Id = Guid.NewGuid(), Name = name, IsDefaultWarframeLayout = false };

    public static ShawzinKeybindProfile CreateDefault() => new()
    {
        Name = "Default Warframe layout",
        IsDefaultWarframeLayout = true,
        Bindings = new Dictionary<ShawzinInputBinding, string>
        {
            [ShawzinInputBinding.String1] = "1", [ShawzinInputBinding.String2] = "2", [ShawzinInputBinding.String3] = "3",
            [ShawzinInputBinding.FretLeft] = "Left", [ShawzinInputBinding.FretMiddle] = "Down", [ShawzinInputBinding.FretRight] = "Right",
        },
    };

    public GameInputKey Get(ShawzinInputBinding binding) => new(Bindings[binding]);
}

public sealed record KeybindValidationIssue(string Code, string Message, ShawzinInputBinding? Binding = null);
public sealed record KeybindValidationResult(IReadOnlyList<KeybindValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public interface IKeybindProfileValidator { KeybindValidationResult Validate(ShawzinKeybindProfile profile); }

public sealed class KeybindProfileValidator : IKeybindProfileValidator
{
    private static readonly HashSet<string> ValidNames = BuildValidNames();
    private static readonly ShawzinInputBinding[] Required =
    [ShawzinInputBinding.String1, ShawzinInputBinding.String2, ShawzinInputBinding.String3,
     ShawzinInputBinding.FretLeft, ShawzinInputBinding.FretMiddle, ShawzinInputBinding.FretRight];

    public KeybindValidationResult Validate(ShawzinKeybindProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<KeybindValidationIssue>();
        if (profile.Id == Guid.Empty) issues.Add(new("EmptyId", "The profile ID is empty."));
        if (string.IsNullOrWhiteSpace(profile.Name)) issues.Add(new("EmptyName", "The profile needs a name."));
        foreach (var binding in Required)
        {
            if (!profile.Bindings.TryGetValue(binding, out var key) || string.IsNullOrWhiteSpace(key))
                issues.Add(new("MissingBinding", $"{binding} is not bound.", binding));
            else if (!ValidNames.Contains(key))
                issues.Add(new("InvalidKey", $"'{key}' is not a supported key name.", binding));
        }
        foreach (var conflict in profile.Bindings.Where(x => !string.IsNullOrWhiteSpace(x.Value)).GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            issues.Add(new("BindingConflict", $"Key '{conflict.Key}' is assigned more than once."));
        return new(issues);
    }

    private static HashSet<string> BuildValidNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Left", "Right", "Up", "Down", "Space", "Enter", "Tab", "Escape", "Shift", "Control", "Alt" };
        foreach (var value in Enumerable.Range('A', 26)) names.Add(((char)value).ToString());
        foreach (var value in Enumerable.Range(0, 10)) names.Add(value.ToString());
        foreach (var value in Enumerable.Range(1, 12)) names.Add($"F{value}");
        return names;
    }
}
