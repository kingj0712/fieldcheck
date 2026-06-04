using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Xunit;

namespace FieldCheck.Tests;

public class MarkdownReportServiceTests
{
    private static readonly DateTime Exported = new(2026, 6, 4, 14, 30, 0);

    private static Checklist Sample()
    {
        var c = new Checklist { Id = "c1", Name = "AHU-1 Checkout" };
        c.Items.Add(new ChecklistItem { Id = "i1", Text = "Verify supply fan", Section = "Fan", Order = 1, Status = ItemStatus.Open, Notes = "Command from BAS" });
        c.Items.Add(new ChecklistItem { Id = "i2", Text = "Verify SAT sensor", Section = "Sensors", Order = 2, Status = ItemStatus.Complete, Completed = true, CompletedAt = new DateTime(2026, 6, 2, 9, 0, 0) });
        c.Items.Add(new ChecklistItem { Id = "i3", Text = "Verify return fan", Section = "Fan", Order = 3, Status = ItemStatus.Issue, IssueNote = "VFD faulted" });
        c.Items.Add(new ChecklistItem { Id = "i4", Text = "Verify economizer", Section = "Economizer", Order = 4, Status = ItemStatus.NotApplicable });
        return c;
    }

    [Fact]
    public void ChecklistReport_IncludesHeaderAndSummary()
    {
        var md = MarkdownReportService.ChecklistReport("Riverside", Sample(), Exported);
        Assert.Contains("# FieldCheck Report", md);
        Assert.Contains("Project: Riverside", md);
        Assert.Contains("Checklist: AHU-1 Checkout", md);
        Assert.Contains("Exported: 2026-06-04 14:30", md);
        Assert.Contains("Progress: 1 of 4 complete", md);
        Assert.Contains("Issues: 1", md);
        Assert.Contains("Open: 1", md);
        Assert.Contains("N/A: 1", md);
    }

    [Fact]
    public void ChecklistReport_HasAllStatusSections()
    {
        var md = MarkdownReportService.ChecklistReport("P", Sample(), Exported);
        Assert.Contains("## Issues", md);
        Assert.Contains("## Open Items", md);
        Assert.Contains("## Completed Items", md);
        Assert.Contains("## N/A Items", md);
    }

    [Fact]
    public void ChecklistReport_IncludesNotesIssueNotesAndCompletedAt()
    {
        var md = MarkdownReportService.ChecklistReport("P", Sample(), Exported);
        Assert.Contains("Notes: Command from BAS", md);
        Assert.Contains("Issue Note: VFD faulted", md);
        Assert.Contains("Completed: 2026-06-02 09:00", md);
    }

    [Fact]
    public void ChecklistReport_UsesStatusMarkers()
    {
        var md = MarkdownReportService.ChecklistReport("P", Sample(), Exported);
        Assert.Contains("- [!] Verify return fan", md); // issue
        Assert.Contains("- [ ] Verify supply fan", md); // open
        Assert.Contains("- [x] Verify SAT sensor", md); // complete
        Assert.Contains("- [/] Verify economizer", md); // n/a
    }

    [Fact]
    public void ChecklistReport_EscapesMarkdownSpecials()
    {
        var c = new Checklist { Id = "c", Name = "X" };
        c.Items.Add(new ChecklistItem { Id = "i", Text = "Check *pressure* [zone]", Section = "General", Order = 1, Status = ItemStatus.Open });
        var md = MarkdownReportService.ChecklistReport("P", c, Exported);
        Assert.Contains(@"Check \*pressure\* \[zone\]", md);
    }

    [Fact]
    public void ChecklistReport_PreservesItemOrderWithinSection()
    {
        var c = new Checklist { Id = "c", Name = "X" };
        c.Items.Add(new ChecklistItem { Id = "a", Text = "First open", Section = "S", Order = 1, Status = ItemStatus.Open });
        c.Items.Add(new ChecklistItem { Id = "b", Text = "Second open", Section = "S", Order = 2, Status = ItemStatus.Open });
        var md = MarkdownReportService.ChecklistReport("P", c, Exported);
        Assert.True(md.IndexOf("First open", StringComparison.Ordinal) < md.IndexOf("Second open", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectReport_IncludesProjectAndPerChecklistIssues()
    {
        var project = new Project { Id = "p", Name = "Riverside", Checklists = { Sample() } };
        var md = MarkdownReportService.ProjectReport(project, Exported);
        Assert.Contains("# FieldCheck Project Report", md);
        Assert.Contains("Project: Riverside", md);
        Assert.Contains("### AHU-1 Checkout", md);
        Assert.Contains("VFD faulted", md); // the issue surfaces in the project summary
    }
}
