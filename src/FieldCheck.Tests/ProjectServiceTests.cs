using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Xunit;

namespace FieldCheck.Tests;

public class ProjectServiceTests
{
    private readonly FakeClock _clock = FakeClock.Default;
    private readonly SequentialIdGenerator _ids = new();

    private Project AddProject(AppState state, string name)
    {
        var project = ProjectService.CreateProject(name, _clock, _ids);
        ProjectService.AddProject(state, project);
        return project;
    }

    [Fact]
    public void CreateProject_SetsFields()
    {
        var project = ProjectService.CreateProject("  Commissioning  ", _clock, _ids);
        Assert.Equal("Commissioning", project.Name);
        Assert.StartsWith("project-", project.Id);
        Assert.False(project.Collapsed);
    }

    [Fact]
    public void CreateProject_BlankName_Throws()
        => Assert.Throws<ArgumentException>(() => ProjectService.CreateProject("  ", _clock, _ids));

    [Fact]
    public void AddProject_NormalizesOrder()
    {
        var state = new AppState();
        AddProject(state, "A");
        AddProject(state, "B");
        Assert.Equal(new[] { 1, 2 }, state.Projects.Select(p => p.Order).ToArray());
    }

    [Fact]
    public void RenameProject_UpdatesNameAndTimestamp()
    {
        var state = new AppState();
        var p = AddProject(state, "A");
        _clock.Advance(TimeSpan.FromMinutes(5));
        ProjectService.RenameProject(p, "Renamed", _clock);
        Assert.Equal("Renamed", p.Name);
        Assert.Equal(_clock.Now, p.UpdatedAt);
    }

    [Fact]
    public void RenameProject_BlankName_Throws()
    {
        var state = new AppState();
        var p = AddProject(state, "A");
        Assert.Throws<ArgumentException>(() => ProjectService.RenameProject(p, "  ", _clock));
    }

    [Fact]
    public void RenameProject_TrimsWhitespace()
    {
        var state = new AppState();
        var p = AddProject(state, "A");
        ProjectService.RenameProject(p, "   Renamed Job   ", _clock);
        Assert.Equal("Renamed Job", p.Name);
    }

    [Fact]
    public void GetProgress_AggregatesAcrossChecklists()
    {
        var project = new Project
        {
            Id = "p1", Name = "Job",
            Checklists =
            {
                new Checklist { Id = "c1", Name = "A", Items = { Item(true), Item(false), Item(false) } },
                new Checklist { Id = "c2", Name = "B", Items = { Item(true), Item(true) } }
            }
        };

        var progress = ProjectService.GetProgress(project);
        Assert.Equal(2, progress.ChecklistCount);
        Assert.Equal(5, progress.TotalItems);
        Assert.Equal(3, progress.CompletedItems);
        Assert.Equal(2, progress.OpenItems);
    }

    [Fact]
    public void GetProgress_EmptyProject_IsAllZero()
    {
        var progress = ProjectService.GetProgress(new Project { Id = "p", Name = "Empty" });
        Assert.Equal(0, progress.ChecklistCount);
        Assert.Equal(0, progress.TotalItems);
        Assert.Equal(0, progress.CompletedItems);
        Assert.Equal(0, progress.OpenItems);
    }

    private static ChecklistItem Item(bool completed) =>
        new() { Id = "i", Text = "t", Section = "General", Completed = completed };

    [Fact]
    public void DeleteProject_RemovesAndRenormalizes()
    {
        var state = new AppState();
        AddProject(state, "A");
        var b = AddProject(state, "B");
        AddProject(state, "C");
        Assert.True(ProjectService.DeleteProject(state, b.Id));
        Assert.Equal(new[] { "A", "C" }, state.Projects.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { 1, 2 }, state.Projects.Select(p => p.Order).ToArray());
    }

    [Fact]
    public void ReorderProjects_UpdatesOrder()
    {
        var state = new AppState();
        AddProject(state, "A");
        AddProject(state, "B");
        AddProject(state, "C");
        ProjectService.ReorderProjects(state.Projects, 0, 2);
        Assert.Equal(new[] { "B", "C", "A" }, state.Projects.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, state.Projects.Select(p => p.Order).ToArray());
    }

    [Fact]
    public void GetOrCreateProject_ReusesExistingCaseInsensitive()
    {
        var state = new AppState();
        var first = ProjectService.GetOrCreateProject(state, "Commissioning", _clock, _ids);
        var again = ProjectService.GetOrCreateProject(state, "commissioning", _clock, _ids);
        Assert.Same(first, again);
        Assert.Single(state.Projects);
    }

    [Fact]
    public void GetOrCreateProject_BlankUsesGeneral()
    {
        var state = new AppState();
        var project = ProjectService.GetOrCreateProject(state, "  ", _clock, _ids);
        Assert.Equal("General", project.Name);
    }

    [Fact]
    public void AddChecklist_NormalizesWithinProject()
    {
        var state = new AppState();
        var p = AddProject(state, "A");
        ProjectService.AddChecklist(p, ChecklistService.CreateChecklist("One", _clock, _ids), _clock);
        ProjectService.AddChecklist(p, ChecklistService.CreateChecklist("Two", _clock, _ids), _clock);
        Assert.Equal(new[] { 1, 2 }, p.Checklists.Select(c => c.Order).ToArray());
    }

    [Fact]
    public void DeleteChecklist_RemovesAndRenumbers()
    {
        var state = new AppState();
        var p = AddProject(state, "A");
        var c1 = ChecklistService.CreateChecklist("One", _clock, _ids);
        var c2 = ChecklistService.CreateChecklist("Two", _clock, _ids);
        ProjectService.AddChecklist(p, c1, _clock);
        ProjectService.AddChecklist(p, c2, _clock);
        Assert.True(ProjectService.DeleteChecklist(p, c1.Id, _clock));
        Assert.Equal(new[] { "Two" }, p.Checklists.Select(c => c.Name).ToArray());
        Assert.Equal(1, p.Checklists[0].Order);
    }

    [Fact]
    public void FindChecklist_ReturnsProjectAndChecklist()
    {
        var state = new AppState();
        var p = AddProject(state, "A");
        var c = ChecklistService.CreateChecklist("One", _clock, _ids);
        ProjectService.AddChecklist(p, c, _clock);

        var found = ProjectService.FindChecklist(state, c.Id);
        Assert.NotNull(found);
        Assert.Same(p, found!.Value.Project);
        Assert.Same(c, found.Value.Checklist);
        Assert.Null(ProjectService.FindChecklist(state, "missing"));
    }
}
