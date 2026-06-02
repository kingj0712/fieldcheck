namespace FieldCheck.Core.Models;

/// <summary>
/// A single checklist line. I keep every item in its parent checklist's <c>Items</c> list
/// regardless of completion state; the <see cref="Completed"/> flag decides whether it appears in
/// the Open, Completed, or All view. Because <see cref="Order"/> is preserved, an unchecked item
/// drops back into its original position.
/// </summary>
public sealed class ChecklistItem
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The required, user-visible text of the item.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Structural grouping. Blank is treated as "General" by the UI and services.</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>Optional free-form notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Zero or more non-exclusive metadata tags.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>1-based position within the parent checklist. This is my stable sort key.</summary>
    public int Order { get; set; }

    public bool Completed { get; set; }

    /// <summary>When the item was completed, or null while it is open.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>I make a deep copy (including a fresh tag list) so clones never share references.</summary>
    public ChecklistItem Clone() => new()
    {
        Id = Id,
        Text = Text,
        Section = Section,
        Notes = Notes,
        Tags = new List<string>(Tags),
        Order = Order,
        Completed = Completed,
        CompletedAt = CompletedAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };
}
