using System.Reflection;

namespace VoidNote.Audio.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void MilestoneA_ContainsNoAudioFeatureImplementation() =>
        Assert.Empty(Assembly.Load("VoidNote.Audio").ExportedTypes);
}
