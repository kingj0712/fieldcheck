using System.Text.Json.Serialization;

namespace FieldCheck.Core.Models;

/// <summary>
/// A first-class container that groups related checklists (e.g. a job or building). Projects are
/// the top level of the hierarchy: Project → Checklist → Section → Item → (Notes, Tags).
/// </summary>
public sealed class Project
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Required, non-blank project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>1-based position within the sidebar.</summary>
    public int Order { get; set; }

    /// <summary>Whether the project group is collapsed in the sidebar (persisted UI state).</summary>
    public bool Collapsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Checklist> Checklists { get; set; } = new();

    [JsonIgnore]
    public int ChecklistCount => Checklists.Count;

    /// <summary>Deep copy with the same ids (callers regenerate ids when duplicating).</summary>
    public Project Clone() => new()
    {
        Id = Id,
        Name = Name,
        Order = Order,
        Collapsed = Collapsed,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Checklists = Checklists.Select(c => c.Clone()).ToList()
    };
}
