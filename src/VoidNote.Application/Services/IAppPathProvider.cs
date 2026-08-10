namespace VoidNote.Application.Services;

/// <summary>Provides application-owned paths without exposing platform APIs to the domain.</summary>
public interface IAppPathProvider
{
    /// <summary>Gets the settings file path.</summary>
    string SettingsFilePath { get; }

    /// <summary>Gets the independently versioned GameBridge profile file path.</summary>
    string GameBridgeProfilesFilePath { get; }

    /// <summary>Gets the local log directory.</summary>
    string LogDirectory { get; }
}
