using System.Globalization;
using FieldCheck.Core.Models;
using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>Exports a single checklist (open and completed items) to CSV, ordered by item order.</summary>
public static class CsvExportService
{
    public static readonly string[] Headers =
    {
        "project_name", "checklist_name", "section", "item_text", "notes", "tags",
        "status", "issue_note", "completed", "completed_at", "order"
    };

    public static string Export(string projectName, Checklist checklist)
    {
        var rows = new List<IEnumerable<string?>> { Headers };

        foreach (var item in checklist.Items.OrderBy(i => i.Order))
        {
            rows.Add(new string?[]
            {
                projectName,
                checklist.Name,
                item.Section,
                item.Text,
                item.Notes,
                ChecklistService.FormatTags(item.Tags),
                ItemStatusText.Label(item.Status),
                item.IssueNote,
                item.Completed ? "true" : "false",
                item.CompletedAt?.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                item.Order.ToString(CultureInfo.InvariantCulture)
            });
        }

        return Csv.Build(rows);
    }
}
