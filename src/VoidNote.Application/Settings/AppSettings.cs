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

    /// <summary>Gets audio decoder, output and cache preferences.</summary>
    public AudioSettings Audio { get; init; } = new();

    /// <summary>Gets optional local worker and resource preferences.</summary>
    public AudioIntelligenceSettings AudioIntelligence { get; init; } = new();

    /// <summary>Gets local opt-in and timing preferences for the optional GameBridge.</summary>
    public GameBridgeSettings GameBridge { get; init; } = new();
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

public sealed record AudioSettings
{
    public string? FfmpegExecutablePath { get; init; }
    public string? FfprobeExecutablePath { get; init; }
    public string? FfplayExecutablePath { get; init; }
}

public sealed record AudioIntelligenceSettings
{
    public string? PythonExecutablePath { get; init; }
    public string? WorkerScriptPath { get; init; }
    public int MaximumParallelJobs { get; init; } = 1;
    public int WorkerTimeoutMinutes { get; init; } = 120;
    public string SeparationEngine { get; init; } = "demucs";
    public string TranscriptionEngine { get; init; } = "basic-pitch";
}

/// <summary>Contains safe defaults for the optional external-input adapter.</summary>
public sealed record GameBridgeSettings
{
    public bool DisclaimerAcknowledged { get; init; }
    public TargetFocusLossBehavior FocusLossBehavior { get; init; } = TargetFocusLossBehavior.Abort;
    public string TargetWindowTitle { get; init; } = "Warframe";
    public int KeyDownLeadMilliseconds { get; init; } = 5;
    public int HoldDurationMilliseconds { get; init; } = 25;
    public int ReleaseDelayMilliseconds { get; init; } = 5;
}

/// <summary>Controls whether loss of the configured target focus aborts real input.</summary>
public enum TargetFocusLossBehavior { Abort, Ignore }
