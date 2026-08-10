using VoidNote.Application.Diagnostics;
using VoidNote.Application.Projects;
using VoidNote.Application.Settings;

namespace VoidNote.IntegrationTests;

public sealed class ReleaseInfrastructureTests
{
    [Fact]
    public void AutosaveIntervals_AreBoundedAndDisabledIsExplicit()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, new AutosaveSettings { Interval = AutosaveInterval.Disabled }.GetInterval());
        Assert.Equal(TimeSpan.FromMinutes(1), new AutosaveSettings { Interval = AutosaveInterval.OneMinute }.GetInterval());
        Assert.Equal(TimeSpan.FromMinutes(5), new AutosaveSettings { Interval = AutosaveInterval.FiveMinutes }.GetInterval());
        Assert.Equal(TimeSpan.FromMinutes(10), new AutosaveSettings { Interval = AutosaveInterval.TenMinutes }.GetInterval());
        Assert.Equal(TimeSpan.FromMinutes(1440), new AutosaveSettings { Interval = AutosaveInterval.Custom, CustomIntervalMinutes = int.MaxValue }.GetInterval());
    }

    [Fact]
    public void RecentProjects_DeduplicatesAndBoundsHistory()
    {
        using var directory = new TemporaryDirectory();
        var values = Enumerable.Range(0, 20)
            .Select(index => new RecentProjectSettings { Name = $"P{index}", Path = Path.Combine(directory.Path, $"{index}.vns"), LastOpenedUtc = DateTimeOffset.UtcNow.AddMinutes(-index) });

        var result = RecentProjects.AddOrUpdate(values, "Newest", Path.Combine(directory.Path, "4.vns"), DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(RecentProjects.MaximumCount, result.Count);
        Assert.Equal("Newest", result[0].Name);
        Assert.Single(result, value => value.Path.EndsWith("4.vns", StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticReport_ExportsTextAndJsonWithoutPrivateProjectData()
    {
        var report = new VoidNoteDiagnosticReport(DateTimeOffset.UnixEpoch, "1.0.0-rc1",
            [new("dotnet", ".NET Runtime", DiagnosticState.Available, ".NET 10", "10.0")]);

        Assert.Contains("VoidNote Diagnostics 1.0.0-rc1", report.ToText());
        Assert.Contains("\"ApplicationVersion\": \"1.0.0-rc1\"", report.ToJson());
    }
}
