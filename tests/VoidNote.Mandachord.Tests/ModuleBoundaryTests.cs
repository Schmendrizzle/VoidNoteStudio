using System.Reflection;
using VoidNote.Domain.Mandachord;
using VoidNote.Mandachord.Generation;

namespace VoidNote.Mandachord.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact] public void Domain_RemainsFreeOfAvaloniaMidiLibrariesAndGameBridge()
    {
        var names = typeof(MandachordArrangement).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();
        Assert.DoesNotContain(names, value => value!.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Melanchall.DryWetMidi", names); Assert.DoesNotContain("VoidNote.GameBridge", names);
    }
    [Fact] public void MandachordModule_ContainsNoGameBridgeOrOsInputTypes()
    {
        var assembly = typeof(MandachordGenerator).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), value => value.Name is "VoidNote.GameBridge");
        Assert.DoesNotContain(assembly.ExportedTypes, value => value.Name.Contains("InputBridge", StringComparison.Ordinal));
    }
}
