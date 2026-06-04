using System.Globalization;
using System.Text;
using FieldCheck.Core.Models;
using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>
/// Writes the whole workspace to a normal folder a user can browse, email, or zip: the raw
/// data.json, a CSV template, and per-project folders holding each checklist as CSV and Markdown
/// plus a project summary. Pure file IO with no external dependency. File and folder names are
/// sanitized so invalid Windows characters never break the export.
/// </summary>
public static class WorkspaceExportService
{
    /// <summary>
    /// Exports into <paramref name="exportFolder"/> (created if needed; the caller is responsible
    /// for confirming an overwrite if it already exists). Returns the folder it wrote to.
    /// </summary>
    public static string Export(AppState state, string? rawDataJson, string exportFolder, DateTime exportedAt)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        if (string.IsNullOrWhiteSpace(exportFolder)) throw new ArgumentException("Export folder is required.", nameof(exportFolder));

        Directory.CreateDirectory(exportFolder);

        // 1. Raw data.json — prefer the live file's text; fall back to serializing current state.
        var dataJson = string.IsNullOrWhiteSpace(rawDataJson)
            ? System.Text.Json.JsonSerializer.Serialize(state, AppJson.Options)
            : rawDataJson;
        File.WriteAllText(Path.Combine(exportFolder, "data.json"), dataJson);

        // 2. CSV template.
        var templatesDir = Path.Combine(exportFolder, "templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(Path.Combine(templatesDir, "csv-template.csv"), TemplateService.GetTemplateCsv());

        // 3. Per-project folders with checklist CSV + Markdown and a project summary.
        var projectsDir = Path.Combine(exportFolder, "projects");
        Directory.CreateDirectory(projectsDir);

        var usedProjectFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in state.Projects.OrderBy(p => p.Order))
        {
            var projectFolderName = UniqueName(usedProjectFolders, SanitizeFileName(project.Name));
            var projectDir = Path.Combine(projectsDir, projectFolderName);
            Directory.CreateDirectory(projectDir);

            File.WriteAllText(Path.Combine(projectDir, "project-summary.md"),
                MarkdownReportService.ProjectReport(project, exportedAt));

            var usedChecklistFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var checklist in project.Checklists.OrderBy(c => c.Order))
            {
                var baseName = UniqueName(usedChecklistFiles, SanitizeFileName(checklist.Name));
                File.WriteAllText(Path.Combine(projectDir, baseName + ".csv"),
                    CsvExportService.Export(project.Name, checklist));
                File.WriteAllText(Path.Combine(projectDir, baseName + ".md"),
                    MarkdownReportService.ChecklistReport(project.Name, checklist, exportedAt));
            }
        }

        // 4. A README explaining the contents.
        File.WriteAllText(Path.Combine(exportFolder, "README.md"), BuildReadme(state, exportedAt));

        return exportFolder;
    }

    /// <summary>Suggested timestamped folder name, e.g. "FieldCheck-Export-2026-06-04-1430".</summary>
    public static string SuggestedFolderName(DateTime timestamp) =>
        "FieldCheck-Export-" + timestamp.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture);

    /// <summary>Replaces characters Windows forbids in a file/folder name and trims trailing dots/spaces.</summary>
    public static string SanitizeFileName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return "Untitled";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);

        var cleaned = sb.ToString().TrimEnd('.', ' ');
        return cleaned.Length == 0 ? "Untitled" : cleaned;
    }

    private static string UniqueName(HashSet<string> used, string desired)
    {
        if (used.Add(desired))
            return desired;
        for (int i = 2; ; i++)
        {
            var candidate = $"{desired} ({i})";
            if (used.Add(candidate))
                return candidate;
        }
    }

    private static string BuildReadme(AppState state, DateTime exportedAt)
    {
        var projectCount = state.Projects.Count;
        var checklistCount = state.Projects.Sum(p => p.Checklists.Count);
        var itemCount = state.Projects.Sum(p => p.Checklists.Sum(c => c.Items.Count));

        var sb = new StringBuilder();
        sb.AppendLine("# FieldCheck Workspace Export");
        sb.AppendLine();
        sb.AppendLine($"Exported: {exportedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Projects: {projectCount}");
        sb.AppendLine($"Checklists: {checklistCount}");
        sb.AppendLine($"Items: {itemCount}");
        sb.AppendLine();
        sb.AppendLine("## What's in this folder");
        sb.AppendLine();
        sb.AppendLine("- `data.json` — the complete raw workspace, exactly as FieldCheck stores it. This is the");
        sb.AppendLine("  authoritative copy; everything else is generated from it for convenience.");
        sb.AppendLine("- `projects/<project>/` — one folder per project. Each contains:");
        sb.AppendLine("  - `project-summary.md` — a Markdown overview of the project and its open issues.");
        sb.AppendLine("  - `<checklist>.csv` — the checklist as CSV (re-importable into FieldCheck).");
        sb.AppendLine("  - `<checklist>.md` — a readable Markdown report grouped by status.");
        sb.AppendLine("- `templates/csv-template.csv` — the blank import template with all supported columns.");
        sb.AppendLine();
        sb.AppendLine("Item statuses are Open, Complete, Issue, and N/A. Items marked Issue include an issue note.");
        return sb.ToString().TrimEnd() + "\n";
    }
}
