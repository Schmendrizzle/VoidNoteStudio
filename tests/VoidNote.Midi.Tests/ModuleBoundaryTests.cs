using System.Reflection;

namespace VoidNote.Midi.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void MilestoneA_ContainsNoMidiFeatureImplementation() =>
        Assert.Empty(Assembly.Load("VoidNote.Midi").ExportedTypes);
}
