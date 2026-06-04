using System.Globalization;
using System.Text;
using FieldCheck.Core.Models;

namespace FieldCheck.Core.Services;

/// <summary>
/// Renders clean, paste-friendly Markdown reports for a checklist or a whole project. Output is
/// plain text suitable for email, Teams, GitHub issues, or project notes — no external dependency.
/// Item order is preserved within each status group.
/// </summary>
public static class MarkdownReportService
{
    /// <summary>A full checklist report: header summary plus Issues / Open / Completed / N/A sections.</summary>
    public static string ChecklistReport(string projectName, Checklist checklist, DateTime exportedAt)
    {
        var sb = new StringBuilder();
        var total = checklist.Items.Count;
        var complete = ChecklistService.CountByStatus(checklist, ItemStatus.Complete);
        var issues = ChecklistService.CountByStatus(checklist, ItemStatus.Issue);
        var open = ChecklistService.CountByStatus(checklist, ItemStatus.Open);
        var na = ChecklistService.CountByStatus(checklist, ItemStatus.NotApplicable);

        sb.AppendLine("# FieldCheck Report");
        sb.AppendLine();
        sb.AppendLine($"Project: {Inline(projectName)}");
        sb.AppendLine($"Checklist: {Inline(checklist.Name)}");
        sb.AppendLine($"Exported: {FormatTimestamp(exportedAt)}");
        sb.AppendLine($"Progress: {complete} of {total} complete");
        sb.AppendLine($"Issues: {issues}");
        sb.AppendLine($"Open: {open}");
        sb.AppendLine($"N/A: {na}");
        sb.AppendLine();

        AppendSection(sb, "Issues", ChecklistService.IssueItems(checklist), ItemStatus.Issue);
        AppendSection(sb, "Open Items", ChecklistService.OpenItems(checklist), ItemStatus.Open);
        AppendSection(sb, "Completed Items", ChecklistService.CompletedItems(checklist), ItemStatus.Complete);
        AppendSection(sb, "N/A Items", ChecklistService.NotApplicableItems(checklist), ItemStatus.NotApplicable);

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>A project-level summary: overall totals plus a per-checklist breakdown with its issues.</summary>
    public static string ProjectReport(Project project, DateTime exportedAt)
    {
        var sb = new StringBuilder();
        var progress = ProjectService.GetProgress(project);

        sb.AppendLine("# FieldCheck Project Report");
        sb.AppendLine();
        sb.AppendLine($"Project: {Inline(project.Name)}");
        sb.AppendLine($"Exported: {FormatTimestamp(exportedAt)}");
        sb.AppendLine($"Checklists: {progress.ChecklistCount}");
        sb.AppendLine($"Progress: {progress.CompletedItems} of {progress.TotalItems} complete");
        sb.AppendLine($"Issues: {progress.IssueItems}");
        sb.AppendLine($"Open: {progress.OpenItems}");
        sb.AppendLine($"N/A: {progress.NotApplicableItems}");
        sb.AppendLine();
        sb.AppendLine("## Checklists");
        sb.AppendLine();

        if (project.Checklists.Count == 0)
        {
            sb.AppendLine("_No checklists yet._");
            return sb.ToString().TrimEnd() + "\n";
        }

        foreach (var checklist in project.Checklists.OrderBy(c => c.Order))
        {
            var total = checklist.Items.Count;
            var complete = ChecklistService.CountByStatus(checklist, ItemStatus.Complete);
            var issues = ChecklistService.CountByStatus(checklist, ItemStatus.Issue);
            var open = ChecklistService.CountByStatus(checklist, ItemStatus.Open);
            var na = ChecklistService.CountByStatus(checklist, ItemStatus.NotApplicable);

            sb.AppendLine($"### {Inline(checklist.Name)}");
            sb.AppendLine($"Progress: {complete} of {total} complete · Issues: {issues} · Open: {open} · N/A: {na}");

            var issueItems = ChecklistService.IssueItems(checklist).ToList();
            if (issueItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Issues:");
                foreach (var item in issueItems)
                {
                    var note = string.IsNullOrWhiteSpace(item.IssueNote) ? string.Empty : $" — {Inline(item.IssueNote)}";
                    sb.AppendLine($"- [!] {Inline(item.Text)}{note}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static void AppendSection(StringBuilder sb, string heading, IEnumerable<ChecklistItem> items, ItemStatus status)
    {
        sb.AppendLine($"## {heading}");
        sb.AppendLine();

        var marker = Marker(status);
        var any = false;
        foreach (var item in items)
        {
            any = true;
            sb.AppendLine($"- [{marker}] {Inline(item.Text)}");
            sb.AppendLine($"  - Section: {Inline(ChecklistService.NormalizeSection(item.Section))}");
            if (!string.IsNullOrWhiteSpace(item.Notes))
                sb.AppendLine($"  - Notes: {Inline(item.Notes)}");
            if (status == ItemStatus.Issue && !string.IsNullOrWhiteSpace(item.IssueNote))
                sb.AppendLine($"  - Issue Note: {Inline(item.IssueNote)}");
            if (status == ItemStatus.Complete && item.CompletedAt is { } when)
                sb.AppendLine($"  - Completed: {FormatTimestamp(when)}");
        }

        if (!any)
            sb.AppendLine("_None_");
        sb.AppendLine();
    }

    private static char Marker(ItemStatus status) => status switch
    {
        ItemStatus.Complete => 'x',
        ItemStatus.Issue => '!',
        ItemStatus.NotApplicable => '/',
        _ => ' '
    };

    private static string FormatTimestamp(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Makes free text safe to drop into a Markdown bullet: newlines collapse to spaces and the
    /// characters most likely to corrupt inline formatting are backslash-escaped.
    /// </summary>
    private static string Inline(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var collapsed = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
        var sb = new StringBuilder(collapsed.Length + 8);
        foreach (var c in collapsed)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '|' or '#')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
