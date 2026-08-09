using VoidNote.Domain.Projects;

namespace VoidNote.Domain.Tests;

public sealed class VoidNoteProjectTests
{
    [Fact]
    public void Validate_AcceptsNewProject()
    {
        var project = new VoidNoteProject();

        project.Validate();

        Assert.Equal(VoidNoteProject.CurrentFormatVersion, project.FormatVersion);
    }

    [Fact]
    public void Validate_RejectsDuplicateItemIdsAcrossModules()
    {
        var duplicate = Guid.NewGuid();
        var project = new VoidNoteProject
        {
            AudioSources = [new AudioSource { Id = duplicate, Name = "Source" }],
            MidiTracks = [new MidiTrack { Id = duplicate, Name = "Track" }],
        };

        Assert.Throws<InvalidOperationException>(project.Validate);
    }

    [Fact]
    public void ProjectFileReference_MakesAbsolutePathsExplicit()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProjectFileReference(Path.GetFullPath("song.wav"), ProjectPathKind.Relative));
    }
}
