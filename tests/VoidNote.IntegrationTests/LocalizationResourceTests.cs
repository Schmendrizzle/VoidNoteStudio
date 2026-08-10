using System.Xml.Linq;

namespace VoidNote.IntegrationTests;

public sealed class LocalizationResourceTests
{
    [Fact]
    public void GermanAndEnglishResourcesHaveIdenticalKeys()
    {
        var root = FindRepositoryRoot();
        var english = Keys(Path.Combine(root, "src", "VoidNote.App", "Resources", "Strings.en.axaml"));
        var german = Keys(Path.Combine(root, "src", "VoidNote.App", "Resources", "Strings.de.axaml"));

        Assert.Equal(english.Order(), german.Order());
        Assert.NotEmpty(english);
    }

    [Fact]
    public void ViewsHaveNoLiteralVisibleTextAttributes()
    {
        var root = FindRepositoryRoot();
        var views = Directory.EnumerateFiles(Path.Combine(root, "src", "VoidNote.App", "Views"), "*.axaml");
        var visibleNames = new HashSet<string> { "Text", "Content", "Header", "Title", "PlaceholderText" };
        var literals = views.SelectMany(path => XDocument.Load(path).Descendants().SelectMany(element => element.Attributes()
                .Where(attribute => visibleNames.Contains(attribute.Name.LocalName) && !attribute.Value.StartsWith('{'))
                .Select(attribute => $"{Path.GetFileName(path)}:{element.Name.LocalName}.{attribute.Name.LocalName}={attribute.Value}")))
            .ToArray();

        Assert.Empty(literals);
    }

    private static HashSet<string> Keys(string path)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(path).Root!.Elements().Select(value => value.Attribute(x + "Key")?.Value).OfType<string>().ToHashSet();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VoidNoteStudio.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
