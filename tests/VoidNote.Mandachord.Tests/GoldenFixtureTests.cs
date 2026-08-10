using System.Text.Json;

namespace VoidNote.Mandachord.Tests;

public sealed class GoldenFixtureTests
{
    [Fact] public void AllRequiredLegalFixtures_ArePresentAndValidJson()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "fixtures"); var files = Directory.GetFiles(directory, "*.json");
        Assert.Equal(10, files.Length); foreach (var file in files) { using var document = JsonDocument.Parse(File.ReadAllText(file)); Assert.True(document.RootElement.TryGetProperty("name", out _)); }
    }
}
