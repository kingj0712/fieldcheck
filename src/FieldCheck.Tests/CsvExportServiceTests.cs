using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using FieldCheck.Core.Utilities;
using Xunit;

namespace FieldCheck.Tests;

public class CsvExportServiceTests
{
    private static Checklist SampleChecklist() => new()
    {
        Id = "checklist-001",
        Name = "AHU_1",
        Items =
        {
            new ChecklistItem
            {
                Id = "item-002", Text = "Second, with comma", Section = "Fan",
                Notes = "Say \"hi\"", Tags = { "AHU-1", "BAS" }, Order = 2, Completed = true,
                CompletedAt = new DateTime(2026, 6, 2, 9, 30, 0)
            },
            new ChecklistItem
            {
                Id = "item-001", Text = "First item", Section = "Sensors",
                Notes = "", Tags = { "Sensor" }, Order = 1, Completed = false
            }
        }
    };

    [Fact]
    public void Export_WritesExpectedHeaderRow()
    {
        var rows = Csv.Parse(CsvExportService.Export("Commissioning", SampleChecklist()));
        Assert.Equal(
            new[] { "project_name", "checklist_name", "section", "item_text", "notes", "tags", "completed", "completed_at", "order" },
            rows[0]);
    }

    [Fact]
    public void Export_IncludesProjectAndChecklistNames()
    {
        var rows = Csv.Parse(CsvExportService.Export("Commissioning", SampleChecklist()));
        Assert.Equal("Commissioning", rows[1][0]);
        Assert.Equal("AHU_1", rows[1][1]);
    }

    [Fact]
    public void Export_PreservesOrder()
    {
        var rows = Csv.Parse(CsvExportService.Export("Commissioning", SampleChecklist()));
        Assert.Equal("First item", rows[1][3]);
        Assert.Equal("Second, with comma", rows[2][3]);
        Assert.Equal("1", rows[1][8]);
        Assert.Equal("2", rows[2][8]);
    }

    [Fact]
    public void Export_WritesTagsSemicolonSeparated()
    {
        var rows = Csv.Parse(CsvExportService.Export("Commissioning", SampleChecklist()));
        Assert.Equal("Sensor", rows[1][5]);
        Assert.Equal("AHU-1; BAS", rows[2][5]);
    }

    [Fact]
    public void Export_IncludesCompletedAndIncompleteItems()
    {
        var rows = Csv.Parse(CsvExportService.Export("Commissioning", SampleChecklist()));
        Assert.Equal(3, rows.Count);
        Assert.Equal("false", rows[1][6]);
        Assert.Equal("true", rows[2][6]);
        Assert.Equal("", rows[1][7]);
        Assert.Equal("2026-06-02T09:30:00", rows[2][7]);
    }

    [Fact]
    public void Export_EscapesCommasAndQuotes()
    {
        var csv = CsvExportService.Export("Commissioning", SampleChecklist());
        Assert.Contains("\"Second, with comma\"", csv);
        Assert.Contains("\"Say \"\"hi\"\"\"", csv);
    }

    [Fact]
    public void Export_RoundTripsBackThroughImport()
    {
        var csv = CsvExportService.Export("Commissioning", SampleChecklist());
        var state = new AppState();
        CsvImportService.Import(csv, "x.csv", state, FakeClock.Default, new SequentialIdGenerator());

        var project = Assert.Single(state.Projects);
        Assert.Equal("Commissioning", project.Name);
        var checklist = Assert.Single(project.Checklists);
        Assert.Equal("AHU_1", checklist.Name);
        Assert.Equal(2, checklist.Items.Count);
        Assert.Contains(checklist.Items, i => i.Tags.SequenceEqual(new[] { "AHU-1", "BAS" }));
    }
}
