namespace FieldCheck.Core.Models;

/// <summary>
/// The field-verification state of a checklist item. <see cref="Open"/> is the default for a new
/// item. The legacy boolean <c>completed</c> maps to <see cref="Open"/> (false) and
/// <see cref="Complete"/> (true); see the storage migration. Items keep a single <c>Order</c>
/// regardless of status, so changing status never reorders an item.
/// </summary>
public enum ItemStatus
{
    /// <summary>Not yet verified.</summary>
    Open,

    /// <summary>Verified / passed. Mirrors the legacy <c>completed = true</c> and stamps CompletedAt.</summary>
    Complete,

    /// <summary>A problem was found; the item carries an issue note describing it.</summary>
    Issue,

    /// <summary>Not applicable to this piece of equipment / scope.</summary>
    NotApplicable
}

/// <summary>Human/CSV text for <see cref="ItemStatus"/> and tolerant parsing for CSV import.</summary>
public static class ItemStatusText
{
    /// <summary>Short, user-facing label (e.g. "N/A"). Used in tabs, badges, reports, and CSV.</summary>
    public static string Label(ItemStatus status) => status switch
    {
        ItemStatus.Complete => "Complete",
        ItemStatus.Issue => "Issue",
        ItemStatus.NotApplicable => "N/A",
        _ => "Open"
    };

    /// <summary>
    /// Parses a CSV/user status value, accepting common spellings (Complete/Completed, N/A/NA/Not
    /// Applicable, etc.). Returns false for blank or unrecognized values so the caller can default
    /// to Open and report it.
    /// </summary>
    public static bool TryParse(string? text, out ItemStatus status)
    {
        status = ItemStatus.Open;
        var t = (text ?? string.Empty).Trim();
        if (t.Length == 0)
            return false;

        switch (t.ToLowerInvariant())
        {
            case "open":
                status = ItemStatus.Open;
                return true;
            case "complete":
            case "completed":
            case "done":
                status = ItemStatus.Complete;
                return true;
            case "issue":
            case "fail":
            case "failed":
                status = ItemStatus.Issue;
                return true;
            case "n/a":
            case "na":
            case "not applicable":
            case "notapplicable":
                status = ItemStatus.NotApplicable;
                return true;
            default:
                return false;
        }
    }
}
