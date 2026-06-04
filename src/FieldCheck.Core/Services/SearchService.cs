using FieldCheck.Core.Models;

namespace FieldCheck.Core.Services;

public enum SearchResultKind
{
    Project,
    Checklist,
    Section,
    Item
}

/// <summary>
/// A single global-search hit with enough context to display it and to navigate to it.
/// Navigation ids are always populated for the levels above the match.
/// </summary>
public sealed class SearchResult
{
    public required SearchResultKind Kind { get; init; }

    /// <summary>The matched name/text (project/checklist/section name, or item text).</summary>
    public required string Title { get; init; }

    /// <summary>Human-readable context path, e.g. "Project / Checklist / Section". Empty for a project.</summary>
    public string PathText { get; init; } = string.Empty;

    /// <summary>Optional short note snippet (item notes), else empty.</summary>
    public string Snippet { get; init; } = string.Empty;

    public required string ProjectId { get; init; }
    public string? ChecklistId { get; init; }
    public string? Section { get; init; }
    public string? ItemId { get; init; }

    /// <summary>For item results: whether the item is completed (used to route to Open vs Completed).</summary>
    public bool ItemCompleted { get; init; }

    public string KindLabel => Kind switch
    {
        SearchResultKind.Project => "Project",
        SearchResultKind.Checklist => "Checklist",
        SearchResultKind.Section => "Section",
        _ => "Item"
    };
}

/// <summary>
/// In-memory, case-insensitive global search across the whole <see cref="AppState"/>: project,
/// checklist and section names, and item text/notes/tags. No index, database, or external service —
/// a simple substring scan that is plenty fast for thousands of items.
/// </summary>
public static class SearchService
{
    public static IReadOnlyList<SearchResult> Search(AppState? state, string? query)
    {
        var results = new List<SearchResult>();
        var term = (query ?? string.Empty).Trim();
        if (state is null || term.Length == 0)
            return results;

        // Group by kind so the palette lists projects, then checklists, then sections, then items.
        var projects = new List<SearchResult>();
        var checklists = new List<SearchResult>();
        var sections = new List<SearchResult>();
        var items = new List<SearchResult>();

        foreach (var project in state.Projects.OrderBy(p => p.Order))
        {
            if (Matches(project.Name, term))
                projects.Add(new SearchResult
                {
                    Kind = SearchResultKind.Project,
                    Title = project.Name,
                    ProjectId = project.Id
                });

            foreach (var checklist in project.Checklists.OrderBy(c => c.Order))
            {
                if (Matches(checklist.Name, term))
                    checklists.Add(new SearchResult
                    {
                        Kind = SearchResultKind.Checklist,
                        Title = checklist.Name,
                        PathText = project.Name,
                        ProjectId = project.Id,
                        ChecklistId = checklist.Id
                    });

                var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in checklist.Items.OrderBy(i => i.Order))
                {
                    var section = ChecklistService.NormalizeSection(item.Section);
                    if (seenSections.Add(section) && Matches(section, term))
                        sections.Add(new SearchResult
                        {
                            Kind = SearchResultKind.Section,
                            Title = section,
                            PathText = $"{project.Name} / {checklist.Name}",
                            ProjectId = project.Id,
                            ChecklistId = checklist.Id,
                            Section = section
                        });
                }

                foreach (var item in checklist.Items.OrderBy(i => i.Order))
                {
                    if (!(Matches(item.Text, term) || Matches(item.Notes, term) || item.Tags.Any(t => Matches(t, term))))
                        continue;

                    var section = ChecklistService.NormalizeSection(item.Section);
                    items.Add(new SearchResult
                    {
                        Kind = SearchResultKind.Item,
                        Title = item.Text,
                        PathText = $"{project.Name} / {checklist.Name} / {section}",
                        Snippet = Snippet(item.Notes),
                        ProjectId = project.Id,
                        ChecklistId = checklist.Id,
                        Section = section,
                        ItemId = item.Id,
                        ItemCompleted = item.Completed
                    });
                }
            }
        }

        results.AddRange(projects);
        results.AddRange(checklists);
        results.AddRange(sections);
        results.AddRange(items);
        return results;
    }

    private static bool Matches(string? value, string term) =>
        !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string Snippet(string? notes)
    {
        var trimmed = (notes ?? string.Empty).Trim();
        return trimmed.Length <= 140 ? trimmed : trimmed[..137] + "…";
    }
}
