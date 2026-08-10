namespace VoidNote.Domain.Projects;

/// <summary>Contains user-facing and audit metadata for a VoidNote project.</summary>
public sealed class ProjectMetadata
{
    /// <summary>Gets or initializes the project title.</summary>
    public string Title { get; set; } = ProjectName.Default;

    /// <summary>Gets or initializes the optional project author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets or initializes when the project was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or initializes when the project was last modified.</summary>
    public DateTimeOffset ModifiedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
