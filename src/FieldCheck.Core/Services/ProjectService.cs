using FieldCheck.Core.Models;
using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>Aggregate status stats for a project, summed across all of its checklists.</summary>
public readonly record struct ProjectProgress(
    int ChecklistCount, int TotalItems, int CompletedItems, int OpenItems, int IssueItems, int NotApplicableItems);

/// <summary>Pure project-container rules: project CRUD/ordering and placing checklists inside projects.</summary>
public static class ProjectService
{
    public const string DefaultProjectName = "General";

    // ---------------------------------------------------------------- projects

    public static Project CreateProject(string name, IClock clock, IIdGenerator ids)
    {
        var now = clock.Now;
        return new Project
        {
            Id = ids.NewId("project"),
            Name = RequireText(name, "Project name"),
            Order = 0,
            Collapsed = false,
            CreatedAt = now,
            UpdatedAt = now,
            Checklists = new List<Checklist>()
        };
    }

    public static void AddProject(AppState state, Project project)
    {
        state.Projects.Add(project);
        NormalizeProjectOrders(state.Projects);
    }

    public static void RenameProject(Project project, string newName, IClock clock)
    {
        project.Name = RequireText(newName, "Project name");
        project.UpdatedAt = clock.Now;
    }

    public static bool DeleteProject(AppState state, string projectId)
    {
        var removed = state.Projects.RemoveAll(p => p.Id == projectId) > 0;
        if (removed)
            NormalizeProjectOrders(state.Projects);
        return removed;
    }

    public static void NormalizeProjectOrders(IList<Project> projects)
    {
        for (int i = 0; i < projects.Count; i++)
            projects[i].Order = i + 1;
    }

    public static void ReorderProjects(IList<Project> projects, int fromIndex, int toIndex)
    {
        ChecklistService.Move(projects, fromIndex, toIndex);
        NormalizeProjectOrders(projects);
    }

    /// <summary>Finds an existing project by name (case-insensitive), or creates and adds one.</summary>
    public static Project GetOrCreateProject(AppState state, string? name, IClock clock, IIdGenerator ids)
    {
        var wanted = string.IsNullOrWhiteSpace(name) ? DefaultProjectName : name.Trim();
        var existing = state.Projects.FirstOrDefault(p => string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var project = CreateProject(wanted, clock, ids);
        AddProject(state, project);
        return project;
    }

    // ---------------------------------------------------------------- checklists within a project

    public static void AddChecklist(Project project, Checklist checklist, IClock clock)
    {
        project.Checklists.Add(checklist);
        NormalizeChecklistOrders(project.Checklists);
        project.UpdatedAt = clock.Now;
    }

    public static bool DeleteChecklist(Project project, string checklistId, IClock clock)
    {
        var removed = project.Checklists.RemoveAll(c => c.Id == checklistId) > 0;
        if (removed)
        {
            NormalizeChecklistOrders(project.Checklists);
            project.UpdatedAt = clock.Now;
        }
        return removed;
    }

    public static void NormalizeChecklistOrders(IList<Checklist> checklists)
    {
        for (int i = 0; i < checklists.Count; i++)
            checklists[i].Order = i + 1;
    }

    public static void ReorderChecklists(IList<Checklist> checklists, int fromIndex, int toIndex)
    {
        ChecklistService.Move(checklists, fromIndex, toIndex);
        NormalizeChecklistOrders(checklists);
    }

    // ---------------------------------------------------------------- lookups

    /// <summary>Locates a checklist and its owning project anywhere in the state.</summary>
    public static (Project Project, Checklist Checklist)? FindChecklist(AppState state, string? checklistId)
    {
        if (string.IsNullOrEmpty(checklistId))
            return null;
        foreach (var project in state.Projects)
        {
            var checklist = project.Checklists.FirstOrDefault(c => c.Id == checklistId);
            if (checklist is not null)
                return (project, checklist);
        }
        return null;
    }

    public static IEnumerable<Checklist> AllChecklists(AppState state) =>
        state.Projects.OrderBy(p => p.Order).SelectMany(p => p.Checklists.OrderBy(c => c.Order));

    /// <summary>Sums item counts by status across every checklist in the project.</summary>
    public static ProjectProgress GetProgress(Project project)
    {
        var items = project.Checklists.SelectMany(c => c.Items).ToList();
        return new ProjectProgress(
            ChecklistCount: project.Checklists.Count,
            TotalItems: items.Count,
            CompletedItems: items.Count(i => i.Status == ItemStatus.Complete),
            OpenItems: items.Count(i => i.Status == ItemStatus.Open),
            IssueItems: items.Count(i => i.Status == ItemStatus.Issue),
            NotApplicableItems: items.Count(i => i.Status == ItemStatus.NotApplicable));
    }

    private static string RequireText(string? value, string fieldName)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException($"{fieldName} cannot be blank.", nameof(value));
        return trimmed;
    }
}
