using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Xunit;

namespace FieldCheck.Tests;

public class CsvImportServiceTests
{
    private static CsvImportResult Import(string csv, AppState state, string fallback = "fallback.csv")
        => CsvImportService.Import(csv, fallback, state, FakeClock.Default, new SequentialIdGenerator());

    [Fact]
    public void ValidSingleProjectSingleChecklist()
    {
        var state = new AppState();
        var csv = "project_name,checklist_name,section,item_text,notes,tags\n" +
                  "Commissioning,AHU_1,Fan,Verify supply fan command,Command fan.,AHU-1; BAS\n" +
                  "Commissioning,AHU_1,Fan,Verify supply fan status,Confirm proof.,AHU-1; BAS";

        var result = Import(csv, state);

        Assert.Equal(1, result.ProjectsCreated);
        Assert.Equal(1, result.ChecklistsCreated);
        Assert.Equal(2, result.ItemsImported);
        var project = Assert.Single(state.Projects);
        Assert.Equal("Commissioning", project.Name);
        var checklist = Assert.Single(project.Checklists);
        Assert.Equal("AHU_1", checklist.Name);
        Assert.Equal(new[] { "AHU-1", "BAS" }, checklist.Items[0].Tags);
        Assert.Equal("Fan", checklist.Items[0].Section);
    }

    [Fact]
    public void ValidMultipleProjectsAndChecklists()
    {
        var state = new AppState();
        var csv = "project_name,checklist_name,item_text\n" +
                  "Proj A,AHU_1,One\n" +
                  "Proj A,Graphics,Two\n" +
                  "Proj B,Alarms,Three";

        var result = Import(csv, state);

        Assert.Equal(2, result.ProjectsCreated);
        Assert.Equal(3, result.ChecklistsCreated);
        Assert.Equal(3, result.ItemsImported);
        var a = state.Projects.Single(p => p.Name == "Proj A");
        Assert.Equal(2, a.Checklists.Count);
        Assert.Single(state.Projects.Single(p => p.Name == "Proj B").Checklists);
    }

    [Fact]
    public void MissingOptionalColumns_FallsBackSensibly()
    {
        var state = new AppState();
        var result = Import("item_text\nVerify fan\nVerify sensor", state, fallback: "morning-rounds");

        Assert.Equal(2, result.ItemsImported);
        var project = Assert.Single(state.Projects);
        Assert.Equal("General", project.Name);
        var checklist = Assert.Single(project.Checklists);
        Assert.Equal("morning-rounds", checklist.Name);
        Assert.All(checklist.Items, i => Assert.Equal("General", i.Section));
        Assert.All(checklist.Items, i => Assert.Empty(i.Tags));
    }

    [Fact]
    public void MissingRequiredItemTextColumn_Throws()
    {
        var ex = Assert.Throws<CsvImportException>(() => Import("project_name,notes\nP,Some notes", new AppState()));
        Assert.Contains("item_text", ex.Message);
    }

    [Fact]
    public void BlankRows_AreIgnored()
    {
        var state = new AppState();
        var result = Import("item_text\nVerify fan\n\n   \nVerify sensor\n", state);
        Assert.Equal(2, result.ItemsImported);
        Assert.Equal(0, result.RowsSkipped);
    }

    [Fact]
    public void BlankItemText_IsSkippedAndReported()
    {
        var state = new AppState();
        var csv = "checklist_name,item_text\nAHU,Verify fan\nAHU,\nAHU,Verify sensor";
        var result = Import(csv, state);
        Assert.Equal(2, result.ItemsImported);
        Assert.Equal(1, result.RowsSkipped);
        Assert.Contains(result.Issues, m => m.Contains("item_text was blank"));
    }

    [Fact]
    public void QuotedCommas_ArePreserved()
    {
        var state = new AppState();
        var csv = "item_text,notes\n\"Verify A, B, and C\",\"Note, with comma\"";
        Import(csv, state);
        var item = state.Projects.Single().Checklists.Single().Items.Single();
        Assert.Equal("Verify A, B, and C", item.Text);
        Assert.Equal("Note, with comma", item.Notes);
    }

    [Fact]
    public void Tags_AreParsedFromSemicolons()
    {
        var state = new AppState();
        Import("item_text,tags\nVerify fan,\"AHU-1; BAS; High Priority\"", state);
        var item = state.Projects.Single().Checklists.Single().Items.Single();
        Assert.Equal(new[] { "AHU-1", "BAS", "High Priority" }, item.Tags);
    }

    [Fact]
    public void ExistingChecklistNameInSameProject_ImportsAsCopy()
    {
        var state = new AppState();
        Import("project_name,checklist_name,item_text\nP,AHU_1,First", state);
        var result = Import("project_name,checklist_name,item_text\nP,AHU_1,Second", state);

        var project = state.Projects.Single(p => p.Name == "P");
        Assert.Equal(2, project.Checklists.Count);
        Assert.Contains(project.Checklists, c => c.Name == "AHU_1");
        Assert.Contains(project.Checklists, c => c.Name == "AHU_1 Copy");
        Assert.Contains(result.Issues, m => m.Contains("already existed"));
    }

    [Fact]
    public void ExistingProject_IsReusedNotDuplicated()
    {
        var state = new AppState();
        Import("project_name,checklist_name,item_text\nCommissioning,AHU_1,First", state);
        var result = Import("project_name,checklist_name,item_text\ncommissioning,Graphics,Second", state);

        Assert.Single(state.Projects); // reused, case-insensitive
        Assert.Equal(0, result.ProjectsCreated);
        Assert.Equal(1, result.ProjectsReused);
        Assert.Equal(2, state.Projects[0].Checklists.Count);
    }

    [Fact]
    public void ExtraColumns_AreIgnored()
    {
        var state = new AppState();
        var result = Import("item_text,priority,extra\nVerify fan,high,foo", state);
        Assert.Equal(1, result.ItemsImported);
        Assert.Equal("Verify fan", state.Projects.Single().Checklists.Single().Items.Single().Text);
    }

    [Fact]
    public void BlankProjectAndSection_BecomeGeneral()
    {
        var state = new AppState();
        Import("project_name,section,item_text\n,,Verify fan", state);
        var project = Assert.Single(state.Projects);
        Assert.Equal("General", project.Name);
        Assert.Equal("General", project.Checklists.Single().Items.Single().Section);
    }

    [Fact]
    public void HeaderMatchingIsCaseInsensitiveAndOrderIndependent()
    {
        var state = new AppState();
        Import("Tags,Item_Text,Section,Checklist_Name,Project_Name\nAHU-1,Verify fan,Fan,AHU_1,Comm", state);
        var project = state.Projects.Single();
        Assert.Equal("Comm", project.Name);
        var item = project.Checklists.Single().Items.Single();
        Assert.Equal("Verify fan", item.Text);
        Assert.Equal("Fan", item.Section);
        Assert.Equal(new[] { "AHU-1" }, item.Tags);
    }
}
