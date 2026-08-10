using VoidNote.Shawzin.Codec;

namespace VoidNote.Shawzin.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void DomainAssembly_HasNoUiMidiOrCodecLibraryDependency()
    {
        var references = typeof(Domain.Shawzin.ShawzinSong).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(references, reference => reference.Name == "Melanchall.DryWetMidi");
        Assert.DoesNotContain(references, reference => reference.Name == "VoidNote.Shawzin");
    }

    [Fact]
    public void ShawzinModule_OnlyReferencesDomainAndFrameworkAssemblies()
    {
        var references = typeof(IShawzinCodec).Assembly.GetReferencedAssemblies();
        var voidNoteReferences = references
            .Where(reference => reference.Name?.StartsWith("VoidNote.", StringComparison.Ordinal) == true)
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.Equal(["VoidNote.Domain"], voidNoteReferences);
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(references, reference => reference.Name == "Melanchall.DryWetMidi");
    }
}
