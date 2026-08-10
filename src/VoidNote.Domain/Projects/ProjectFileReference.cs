namespace VoidNote.Domain.Projects;

/// <summary>Identifies whether an asset path is portable or external.</summary>
public enum ProjectPathKind
{
    /// <summary>The path is relative to the project container.</summary>
    Relative,
    /// <summary>The path points to an external absolute location.</summary>
    Absolute,
    /// <summary>The path identifies an entry embedded in the project container.</summary>
    Embedded,
}

/// <summary>Represents an explicit relative or absolute project file reference.</summary>
public sealed record ProjectFileReference
{
    /// <summary>Creates a validated file reference.</summary>
    public ProjectFileReference(string path, ProjectPathKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var isRooted = System.IO.Path.IsPathRooted(path);
        if ((kind == ProjectPathKind.Relative || kind == ProjectPathKind.Embedded) && isRooted)
        {
            throw new ArgumentException("A relative or embedded project path cannot be rooted.", nameof(path));
        }

        if (kind == ProjectPathKind.Absolute && !isRooted)
        {
            throw new ArgumentException("An absolute project path must be rooted.", nameof(path));
        }

        Path = path.Replace('\\', '/');
        Kind = kind;
    }

    /// <summary>Gets the normalized path.</summary>
    public string Path { get; }

    /// <summary>Gets the path kind.</summary>
    public ProjectPathKind Kind { get; }
}
