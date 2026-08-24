using System.Text.Json;
using VoidNote.Application.Services;

namespace VoidNote.GameBridge.Profiles;

public interface IKeybindProfileStore
{
    Task<IReadOnlyList<ShawzinKeybindProfile>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyCollection<ShawzinKeybindProfile> profiles, CancellationToken cancellationToken = default);
}

/// <summary>Persists profiles locally and atomically in an independently versioned document.</summary>
public sealed class JsonKeybindProfileStore(IAppPathProvider paths, IKeybindProfileValidator validator) : IKeybindProfileStore
{
    private const int CurrentSchemaVersion = 2;
    private sealed record Document(int SchemaVersion, IReadOnlyList<ShawzinKeybindProfile> Profiles);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General) { WriteIndented = true };

    public async Task<IReadOnlyList<ShawzinKeybindProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.GameBridgeProfilesFilePath)) return [ShawzinKeybindProfile.CreateDefault()];
        await using var stream = File.OpenRead(paths.GameBridgeProfilesFilePath);
        var document = await JsonSerializer.DeserializeAsync<Document>(stream, Options, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The GameBridge profile document is empty.");
        if (document.SchemaVersion is < 1 or > CurrentSchemaVersion) throw new InvalidDataException($"Unsupported GameBridge profile schema: {document.SchemaVersion}.");
        var profiles = document.Profiles.Select(profile => document.SchemaVersion == 1 ? AddScaleSelect(profile) : profile).ToArray();
        foreach (var profile in profiles)
        {
            var result = validator.Validate(profile);
            if (!result.IsValid && document.SchemaVersion != 1)
                throw new InvalidDataException($"Profile '{profile.Name}' is invalid: {string.Join(" ", result.Issues.Select(x => x.Message))}");
        }
        return profiles;
    }

    public async Task SaveAsync(IReadOnlyCollection<ShawzinKeybindProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Select(x => x.Id).Distinct().Count() != profiles.Count) throw new InvalidDataException("Profile IDs must be unique.");
        foreach (var profile in profiles)
        {
            var result = validator.Validate(profile);
            if (!result.IsValid) throw new InvalidDataException($"Profile '{profile.Name}' is invalid: {string.Join(" ", result.Issues.Select(x => x.Message))}");
        }
        var directory = Path.GetDirectoryName(paths.GameBridgeProfilesFilePath) ?? throw new InvalidOperationException("Profile path has no parent.");
        Directory.CreateDirectory(directory);
        var temporary = paths.GameBridgeProfilesFilePath + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, new Document(CurrentSchemaVersion, profiles.ToArray()), Options, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, paths.GameBridgeProfilesFilePath, true);
        }
        finally { File.Delete(temporary); }
    }

    private static ShawzinKeybindProfile AddScaleSelect(ShawzinKeybindProfile profile)
    {
        if (profile.Bindings.ContainsKey(ShawzinInputBinding.ScaleSelect)) return profile;
        var bindings = profile.Bindings.ToDictionary(value => value.Key, value => value.Value);
        bindings[ShawzinInputBinding.ScaleSelect] = "Tab";
        return profile with { Bindings = bindings };
    }
}

/// <summary>Provides validated CRUD operations without hiding persistence failures.</summary>
public sealed class KeybindProfileService(IKeybindProfileStore store, IKeybindProfileValidator validator)
{
    public Task<IReadOnlyList<ShawzinKeybindProfile>> LoadAsync(CancellationToken cancellationToken = default) => store.LoadAsync(cancellationToken);
    public async Task<IReadOnlyList<ShawzinKeybindProfile>> AddOrUpdateAsync(IReadOnlyCollection<ShawzinKeybindProfile> profiles, ShawzinKeybindProfile profile, CancellationToken token = default)
    {
        var validation = validator.Validate(profile);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Issues.Select(x => x.Message)));
        var result = profiles.Where(x => x.Id != profile.Id).Append(profile).ToArray();
        await store.SaveAsync(result, token).ConfigureAwait(false); return result;
    }
    public Task<IReadOnlyList<ShawzinKeybindProfile>> DuplicateAsync(IReadOnlyCollection<ShawzinKeybindProfile> profiles, Guid id, string name, CancellationToken token = default) =>
        AddOrUpdateAsync(profiles, profiles.Single(x => x.Id == id).Duplicate(name), token);
    public async Task<IReadOnlyList<ShawzinKeybindProfile>> DeleteAsync(IReadOnlyCollection<ShawzinKeybindProfile> profiles, Guid id, CancellationToken token = default)
    {
        var result = profiles.Where(x => x.Id != id).ToArray();
        await store.SaveAsync(result, token).ConfigureAwait(false); return result;
    }
}
