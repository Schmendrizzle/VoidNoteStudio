using System.Reflection;

namespace VoidNote.Mandachord.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void MilestoneA_ContainsNoMandachordFeatureImplementation() =>
        Assert.Empty(Assembly.Load("VoidNote.Mandachord").ExportedTypes);
}
