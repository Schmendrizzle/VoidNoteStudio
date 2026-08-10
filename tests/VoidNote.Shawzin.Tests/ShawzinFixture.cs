namespace VoidNote.Shawzin.Tests;

internal static class ShawzinFixture
{
    public static string Read(string kind, string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", kind, $"{name}.shawzin")).Trim();

    public static IEnumerable<object[]> ValidNames() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Valid"), "*.shawzin")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });
}
