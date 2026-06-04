using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using FieldCheck.Core.Utilities;
using Xunit;

namespace FieldCheck.Tests;

public class TemplateServiceTests
{
    [Fact]
    public void Template_HasExpectedHeader()
    {
        var rows = Csv.Parse(TemplateService.GetTemplateCsv());
        Assert.Equal(
            new[] { "project_name", "checklist_name", "section", "item_text", "notes", "tags", "status", "issue_note" },
            rows[0]);
    }

    [Fact]
    public void Template_ImportsCleanly()
    {
        var state = new AppState();
        var result = CsvImportService.Import(
            TemplateService.GetTemplateCsv(), "template.csv", state, FakeClock.Default, new SequentialIdGenerator());

        Assert.Equal(1, result.ProjectsCreated);                 // Commissioning Checklist
        Assert.Equal(2, result.ChecklistsCreated);               // AHU_1 + Graphics Review
        Assert.Equal(5, result.ItemsImported);
        Assert.Equal(0, result.RowsSkipped);

        var project = Assert.Single(state.Projects);
        Assert.Equal("Commissioning Checklist", project.Name);
        var ahu = project.Checklists.Single(c => c.Name == "AHU_1");
        Assert.Contains(ahu.Items, i => i.Tags.Contains("BAS"));

        // the template demonstrates the status + issue_note columns
        var allItems = project.Checklists.SelectMany(c => c.Items).ToList();
        Assert.Contains(allItems, i => i.Status == ItemStatus.Issue && !string.IsNullOrWhiteSpace(i.IssueNote));
        Assert.Contains(allItems, i => i.Status == ItemStatus.Complete);
        Assert.Contains(allItems, i => i.Status == ItemStatus.NotApplicable);
    }
}
