namespace VoidNote.Application.Settings;

/// <summary>Represents the versioned global settings persisted for a user.</summary>
public sealed record AppSettings
{
    /// <summary>The settings schema version supported by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Gets the serialized settings schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets general application settings.</summary>
    public GeneralSettings General { get; init; } = new();

    /// <summary>Gets appearance settings.</summary>
    public AppearanceSettings Appearance { get; init; } = new();

    /// <summary>Gets storage settings.</summary>
    public StorageSettings Storage { get; init; } = new();
}

/// <summary>Contains settings that apply to the complete application.</summary>
public sealed record GeneralSettings
{
    /// <summary>Gets the preferred UI culture name.</summary>
    public string Culture { get; init; } = "en";
}

/// <summary>Identifies the supported application theme selection.</summary>
public enum ThemePreference
{
    /// <summary>Use the operating-system preference.</summary>
    System,
    /// <summary>Use the light theme.</summary>
    Light,
    /// <summary>Use the dark theme.</summary>
    Dark,
}

/// <summary>Contains visual preferences independent of Avalonia types.</summary>
public sealed record AppearanceSettings
{
    /// <summary>Gets the selected theme.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;
}

/// <summary>Contains user-selected storage locations.</summary>
public sealed record StorageSettings
{
    /// <summary>Gets the optional default project directory.</summary>
    public string? DefaultProjectDirectory { get; init; }
}
