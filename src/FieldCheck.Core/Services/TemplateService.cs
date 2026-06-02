using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>Produces the downloadable starter CSV: the required headers plus a few example rows.</summary>
public static class TemplateService
{
    public static string GetTemplateCsv()
    {
        var rows = new List<IEnumerable<string?>>
        {
            new[] { "project_name", "checklist_name", "section", "item_text", "notes", "tags" },
            new[] { "Commissioning Checklist", "AHU_1", "Fan", "Verify supply fan command", "Command fan from BAS and verify output changes.", "AHU-1; BAS" },
            new[] { "Commissioning Checklist", "AHU_1", "Fan", "Verify supply fan status", "Confirm proof/status follows command.", "AHU-1; BAS" },
            new[] { "Commissioning Checklist", "AHU_1", "Sensors", "Verify SAT sensor", "Compare BAS value to field reading.", "AHU-1; Sensor" },
            new[] { "Commissioning Checklist", "Graphics Review", "Navigation", "Verify AHU graphic links", "Confirm graphic opens correct equipment view.", "Graphics; AHU-1" },
            new[] { "Commissioning Checklist", "Graphics Review", "Alarms", "Verify alarm console link", "Confirm alarm console opens and filters correctly.", "Graphics; Alarms" }
        };

        return Csv.Build(rows);
    }
}
