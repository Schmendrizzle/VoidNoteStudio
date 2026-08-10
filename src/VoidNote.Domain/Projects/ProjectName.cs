namespace VoidNote.Domain.Projects;

/// <summary>Defines the filesystem-independent rules for user-facing project names.</summary>
public static class ProjectName
{
    /// <summary>The invariant name stored for a newly created project.</summary>
    public const string Default = "Untitled";

    /// <summary>The maximum number of UTF-16 characters accepted for a project name.</summary>
    public const int MaximumLength = 120;

    /// <summary>Validates and trims a project name without applying filename restrictions.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A project name cannot be empty.", nameof(value));

        var normalized = value.Trim();
        if (normalized.Length > MaximumLength)
            throw new ArgumentException($"A project name cannot exceed {MaximumLength} characters.", nameof(value));

        return normalized;
    }
}
