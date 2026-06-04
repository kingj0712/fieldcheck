using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>Produces the downloadable starter CSV: the required headers plus a few example rows.</summary>
public static class TemplateService
{
    public static string GetTemplateCsv()
    {
        // status is optional and defaults to Open; valid values: Open, Complete, Issue, N/A.
        // issue_note applies when status is Issue.
        var rows = new List<IEnumerable<string?>>
        {
            new[] { "project_name", "checklist_name", "section", "item_text", "notes", "tags", "status", "issue_note" },
            new[] { "Commissioning Checklist", "AHU_1", "Fan", "Verify supply fan command", "Command fan from BAS and verify output changes.", "AHU-1; BAS", "Open", "" },
            new[] { "Commissioning Checklist", "AHU_1", "Fan", "Verify supply fan status", "Confirm proof/status follows command.", "AHU-1; BAS", "Issue", "Status point stuck off; VFD fault." },
            new[] { "Commissioning Checklist", "AHU_1", "Sensors", "Verify SAT sensor", "Compare BAS value to field reading.", "AHU-1; Sensor", "Complete", "" },
            new[] { "Commissioning Checklist", "Graphics Review", "Navigation", "Verify AHU graphic links", "Confirm graphic opens correct equipment view.", "Graphics; AHU-1", "N/A", "" },
            new[] { "Commissioning Checklist", "Graphics Review", "Alarms", "Verify alarm console link", "Confirm alarm console opens and filters correctly.", "Graphics; Alarms", "Open", "" }
        };

        return Csv.Build(rows);
    }
}
