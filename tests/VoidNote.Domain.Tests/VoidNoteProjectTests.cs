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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ProjectName_RejectsEmptyValues(string value)
    {
        Assert.Throws<ArgumentException>(() => ProjectName.Normalize(value));
    }

    [Fact]
    public void ProjectName_EnforcesDocumentedMaximumLength()
    {
        Assert.Equal(120, ProjectName.MaximumLength);
        Assert.Equal(new string('n', 120), ProjectName.Normalize(new string('n', 120)));
        Assert.Throws<ArgumentException>(() => ProjectName.Normalize(new string('n', 121)));
    }

    [Fact]
    public void ProjectName_DoesNotApplyFileSystemCharacterRestrictions()
    {
        Assert.Equal("Lead: Bass / Drums?", ProjectName.Normalize("Lead: Bass / Drums?"));
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

    [Fact]
    public void ProjectFileReference_AcceptsPlatformAbsolutePath()
    {
        var absolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "VoidNote.Tests", "song.wav"));

        var reference = new ProjectFileReference(absolutePath, ProjectPathKind.Absolute);

        Assert.Equal(ProjectPathKind.Absolute, reference.Kind);
        Assert.True(Path.IsPathRooted(reference.Path));
    }
}
