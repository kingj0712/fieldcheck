using System.Text.Json.Serialization;

namespace FieldCheck.Core.Models;

/// <summary>
/// A named checklist that belongs to one <see cref="Project"/>. Items live here in a single list;
/// completed items are never moved to a separate collection — the <see cref="ChecklistItem.Completed"/>
/// flag and the preserved <see cref="ChecklistItem.Order"/> drive the Open/Completed/All views.
/// </summary>
public sealed class Checklist
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Required, non-blank checklist name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>1-based position within its parent project.</summary>
    public int Order { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ChecklistItem> Items { get; set; } = new();

    [JsonIgnore]
    public int CompletedCount => Items.Count(i => i.Completed);

    [JsonIgnore]
    public int TotalCount => Items.Count;

    /// <summary>Deep-copies items so the clone is fully independent of the source.</summary>
    public Checklist Clone() => new()
    {
        Id = Id,
        Name = Name,
        Order = Order,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Items = Items.Select(i => i.Clone()).ToList()
    };
}
