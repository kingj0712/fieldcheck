using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Xunit;

namespace FieldCheck.Tests;

public sealed class WorkspaceExportServiceTests : IDisposable
{
    private readonly string _dir;

    public WorkspaceExportServiceTests()
        => _dir = Path.Combine(Path.GetTempPath(), "FieldCheckExportTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    [Theory]
    [InlineData("AHU-1 / Floor 3", "AHU-1 _ Floor 3")]
    [InlineData("VAV: 101?", "VAV_ 101_")]
    [InlineData("   ", "Untitled")]
    [InlineData("Normal Name", "Normal Name")]
    public void SanitizeFileName_ReplacesInvalidCharacters(string input, string expected)
        => Assert.Equal(expected, WorkspaceExportService.SanitizeFileName(input));

    [Fact]
    public void Export_WritesAllArtifacts()
    {
        var folder = Path.Combine(_dir, "out");
        WorkspaceExportService.Export(SampleState(), "{\"version\":3}", folder, new DateTime(2026, 6, 4, 14, 30, 0));

        Assert.True(File.Exists(Path.Combine(folder, "data.json")));
        Assert.True(File.Exists(Path.Combine(folder, "README.md")));
        Assert.True(File.Exists(Path.Combine(folder, "templates", "csv-template.csv")));

        var projDir = Path.Combine(folder, "projects", "Riverside Medical Center - BAS Commissioning");
        Assert.True(Directory.Exists(projDir));
        Assert.True(File.Exists(Path.Combine(projDir, "project-summary.md")));
        Assert.True(File.Exists(Path.Combine(projDir, "AHU-1 Checkout.csv")));
        Assert.True(File.Exists(Path.Combine(projDir, "AHU-1 Checkout.md")));

        Assert.Equal("{\"version\":3}", File.ReadAllText(Path.Combine(folder, "data.json")));
    }

    [Fact]
    public void Export_NullRawJson_SerializesCurrentState()
    {
        var folder = Path.Combine(_dir, "out2");
        WorkspaceExportService.Export(SampleState(), null, folder, new DateTime(2026, 6, 4, 14, 30, 0));
        Assert.Contains("Riverside", File.ReadAllText(Path.Combine(folder, "data.json")));
    }

    [Fact]
    public void Export_ChecklistMarkdownIncludesIssueNote()
    {
        var folder = Path.Combine(_dir, "out3");
        WorkspaceExportService.Export(SampleState(), null, folder, new DateTime(2026, 6, 4, 14, 30, 0));
        var md = File.ReadAllText(Path.Combine(folder, "projects",
            "Riverside Medical Center - BAS Commissioning", "AHU-1 Checkout.md"));
        Assert.Contains("Issue Note: fault", md);
    }

    private static AppState SampleState()
    {
        var state = new AppState();
        var project = new Project { Id = "p1", Name = "Riverside Medical Center - BAS Commissioning", Order = 1 };
        var checklist = new Checklist { Id = "c1", Name = "AHU-1 Checkout", Order = 1 };
        checklist.Items.Add(new ChecklistItem
        {
            Id = "i1", Text = "Verify fan", Section = "Fan", Order = 1,
            Status = ItemStatus.Issue, IssueNote = "fault"
        });
        project.Checklists.Add(checklist);
        state.Projects.Add(project);
        return state;
    }
}
