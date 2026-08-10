using System.Reflection;
using VoidNote.Midi.Devices;

namespace VoidNote.Midi.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void PublicMidiApi_DoesNotExposeDryWetMidiTypes()
    {
        var assembly = typeof(IMidiDeviceProvider).Assembly;
        var exposedTypes = assembly.ExportedTypes
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(member => member switch
            {
                MethodInfo method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType),
                PropertyInfo property => [property.PropertyType],
                FieldInfo field => [field.FieldType],
                EventInfo eventInfo => [eventInfo.EventHandlerType!],
                _ => [],
            })
            .Where(type => type.FullName?.StartsWith("Melanchall.", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(exposedTypes);
    }

    [Fact]
    public void DomainAssembly_DoesNotReferenceDryWetMidi()
    {
        var references = typeof(Domain.Music.ProjectTimeline).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name == "Melanchall.DryWetMidi");
    }
}
