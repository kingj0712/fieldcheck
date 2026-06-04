using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Xunit;

namespace FieldCheck.Tests;

public class SearchServiceTests
{
    private const string ProjectName = "Riverside Medical Center - BAS Commissioning";

    private static AppState Sample()
    {
        var state = new AppState();
        state.Projects.Add(new Project
        {
            Id = "project-001", Name = ProjectName, Order = 1,
            Checklists =
            {
                new Checklist
                {
                    Id = "checklist-001", Name = "AHU-1 Checkout", Order = 1,
                    Items =
                    {
                        new ChecklistItem
                        {
                            Id = "item-001", Text = "Verify supply fan command", Section = "Fan",
                            Notes = "Command SF from BAS at 0, 50 and 100%; confirm VFD follows.",
                            Tags = { "AHU-1", "BAS" }, Order = 1, Completed = false
                        },
                        new ChecklistItem
                        {
                            Id = "item-002", Text = "Verify SAT sensor", Section = "Sensors",
                            Notes = "Compare BAS value to field reading.", Tags = { "Sensor" },
                            Order = 2, Completed = true, CompletedAt = new DateTime(2026, 6, 2, 9, 0, 0)
                        }
                    }
                },
                new Checklist
                {
                    Id = "checklist-002", Name = "Graphics Review", Order = 2,
                    Items =
                    {
                        new ChecklistItem { Id = "item-010", Text = "Verify AHU graphic links", Section = "Navigation", Order = 1 }
                    }
                }
            }
        });
        state.Projects.Add(new Project { Id = "project-002", Name = "Sample Project", Order = 2 });
        return state;
    }

    [Fact]
    public void FindsProjectByName()
    {
        var r = SearchService.Search(Sample(), "Riverside");
        Assert.Contains(r, x => x.Kind == SearchResultKind.Project && x.ProjectId == "project-001");
    }

    [Fact]
    public void FindsChecklistByName()
    {
        var r = SearchService.Search(Sample(), "Graphics Review");
        var hit = Assert.Single(r, x => x.Kind == SearchResultKind.Checklist);
        Assert.Equal("Graphics Review", hit.Title);
        Assert.Equal("checklist-002", hit.ChecklistId);
        Assert.Equal(ProjectName, hit.PathText);
    }

    [Fact]
    public void FindsSectionByName()
    {
        var r = SearchService.Search(Sample(), "Sensors");
        var hit = Assert.Single(r, x => x.Kind == SearchResultKind.Section);
        Assert.Equal("Sensors", hit.Title);
        Assert.Equal("checklist-001", hit.ChecklistId);
        Assert.Equal($"{ProjectName} / AHU-1 Checkout", hit.PathText);
    }

    [Fact]
    public void FindsItemByText()
    {
        var r = SearchService.Search(Sample(), "supply fan");
        Assert.Contains(r, x => x.Kind == SearchResultKind.Item && x.ItemId == "item-001");
    }

    [Fact]
    public void FindsItemByNotes()
    {
        var r = SearchService.Search(Sample(), "VFD follows");
        var hit = Assert.Single(r, x => x.Kind == SearchResultKind.Item);
        Assert.Equal("item-001", hit.ItemId);
    }

    [Fact]
    public void FindsItemByTag()
    {
        var r = SearchService.Search(Sample(), "BAS");
        Assert.Contains(r, x => x.Kind == SearchResultKind.Item && x.ItemId == "item-001");
    }

    [Fact]
    public void FindsItemByIssueNote()
    {
        var state = Sample();
        var item = state.Projects[0].Checklists[0].Items.First(i => i.Id == "item-001");
        item.Status = ItemStatus.Issue;
        item.IssueNote = "Damper actuator unresponsive";

        var r = SearchService.Search(state, "actuator unresponsive");
        Assert.Contains(r, x => x.Kind == SearchResultKind.Item && x.ItemId == "item-001");
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        var r = SearchService.Search(Sample(), "verify supply FAN");
        Assert.Contains(r, x => x.ItemId == "item-001");
    }

    [Fact]
    public void MatchesPartialTerms()
    {
        var r = SearchService.Search(Sample(), "fan");
        Assert.Contains(r, x => x.Kind == SearchResultKind.Section && x.Title == "Fan");
        Assert.Contains(r, x => x.Kind == SearchResultKind.Item && x.ItemId == "item-001");
    }

    [Fact]
    public void TrimsWhitespace()
    {
        var r = SearchService.Search(Sample(), "   supply fan   ");
        Assert.Contains(r, x => x.ItemId == "item-001");
    }

    [Fact]
    public void ItemResultCarriesNavigationContext()
    {
        var hit = Assert.Single(SearchService.Search(Sample(), "supply fan command"), x => x.Kind == SearchResultKind.Item);
        Assert.Equal("project-001", hit.ProjectId);
        Assert.Equal("checklist-001", hit.ChecklistId);
        Assert.Equal("Fan", hit.Section);
        Assert.Equal($"{ProjectName} / AHU-1 Checkout / Fan", hit.PathText);
        Assert.Contains("confirm VFD follows", hit.Snippet);
        Assert.False(hit.ItemCompleted);
    }

    [Fact]
    public void CompletedItemResultIdentifiesTabRouting()
    {
        var hit = Assert.Single(SearchService.Search(Sample(), "SAT sensor"), x => x.Kind == SearchResultKind.Item);
        Assert.Equal("item-002", hit.ItemId);
        Assert.True(hit.ItemCompleted); // routes to the Completed tab
        Assert.Equal("checklist-001", hit.ChecklistId);
        Assert.Equal("project-001", hit.ProjectId);
    }

    [Fact]
    public void NoMatches_ReturnsEmpty()
    {
        Assert.Empty(SearchService.Search(Sample(), "zzzz-no-such-term"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankQuery_ReturnsEmpty(string? query)
    {
        Assert.Empty(SearchService.Search(Sample(), query));
    }
}
