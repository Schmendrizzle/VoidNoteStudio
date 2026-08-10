using VoidNote.Application.Settings;

namespace VoidNote.Application.Projects;

public static class RecentProjects
{
    public const int MaximumCount = 12;

    public static IReadOnlyList<RecentProjectSettings> AddOrUpdate(
        IEnumerable<RecentProjectSettings> current,
        string name,
        string path,
        DateTimeOffset openedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return current.Where(value => !comparer.Equals(Path.GetFullPath(value.Path), fullPath))
            .Append(new RecentProjectSettings { Name = name, Path = fullPath, LastOpenedUtc = openedAtUtc })
            .OrderByDescending(value => value.LastOpenedUtc)
            .Take(MaximumCount)
            .ToArray();
    }

    public static bool IsMissing(RecentProjectSettings project) => !File.Exists(project.Path);
}
