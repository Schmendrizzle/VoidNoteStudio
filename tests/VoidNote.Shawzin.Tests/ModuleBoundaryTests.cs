using System.Reflection;

namespace VoidNote.Shawzin.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void MilestoneA_ContainsNoShawzinFeatureImplementation() =>
        Assert.Empty(Assembly.Load("VoidNote.Shawzin").ExportedTypes);
}
