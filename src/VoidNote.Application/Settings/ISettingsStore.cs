namespace VoidNote.Application.Settings;

/// <summary>Loads and saves versioned local application settings.</summary>
public interface ISettingsStore
{
    /// <summary>Loads settings or returns defaults when no settings file exists.</summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves settings without partially replacing a valid settings file.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
