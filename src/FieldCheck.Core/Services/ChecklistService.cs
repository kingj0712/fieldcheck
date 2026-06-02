using FieldCheck.Core.Models;
using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>An ordered group of items that share a section, used for grouped rendering and printing.</summary>
public sealed record SectionItemGroup(string Section, IReadOnlyList<ChecklistItem> Items);

/// <summary>
/// Pure checklist/item/section/tag rules, unit-tested without any UI. The view models call straight
/// into these so behavior is identical between the app and the tests.
/// </summary>
public static class ChecklistService
{
    public const string DefaultSection = "General";

    // ---------------------------------------------------------------- factories

    public static Checklist CreateChecklist(string name, IClock clock, IIdGenerator ids)
    {
        var now = clock.Now;
        return new Checklist
        {
            Id = ids.NewId("checklist"),
            Name = RequireText(name, "Checklist name"),
            Order = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Items = new List<ChecklistItem>()
        };
    }

    public static ChecklistItem CreateItem(string text, string? section, string? notes,
        IEnumerable<string>? tags, IClock clock, IIdGenerator ids)
    {
        var now = clock.Now;
        return new ChecklistItem
        {
            Id = ids.NewId("item"),
            Text = RequireText(text, "Item text"),
            Section = NormalizeSection(section),
            Notes = (notes ?? string.Empty).Trim(),
            Tags = NormalizeTags(tags),
            Order = 0,
            Completed = false,
            CompletedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // ---------------------------------------------------------------- checklist field operations

    public static void Rename(Checklist checklist, string newName, IClock clock)
    {
        checklist.Name = RequireText(newName, "Checklist name");
        checklist.UpdatedAt = clock.Now;
    }

    /// <summary>Creates an independent copy with new ids and a unique name within its project.</summary>
    public static Checklist Duplicate(Checklist source, IEnumerable<string> existingNames, IClock clock, IIdGenerator ids)
    {
        var copy = source.Clone();
        var now = clock.Now;
        copy.Id = ids.NewId("checklist");
        copy.Name = MakeUniqueName(existingNames, source.Name);
        copy.Order = 0;
        copy.CreatedAt = now;
        copy.UpdatedAt = now;
        foreach (var item in copy.Items)
            item.Id = ids.NewId("item");
        return copy;
    }

    /// <summary>Marks every item incomplete again. Order, text, section, and tags are untouched.</summary>
    public static void ResetChecklist(Checklist checklist, IClock clock)
    {
        var now = clock.Now;
        var changed = false;
        foreach (var item in checklist.Items)
        {
            if (!item.Completed && item.CompletedAt is null)
                continue;
            item.Completed = false;
            item.CompletedAt = null;
            item.UpdatedAt = now;
            changed = true;
        }
        if (changed)
            checklist.UpdatedAt = now;
    }

    // ---------------------------------------------------------------- item operations

    public static void AddItem(Checklist checklist, ChecklistItem item, IClock clock)
    {
        checklist.Items.Add(item);
        NormalizeItemOrders(checklist.Items);
        checklist.UpdatedAt = clock.Now;
    }

    public static void EditItem(ChecklistItem item, string text, string? section, string? notes,
        IEnumerable<string>? tags, IClock clock)
    {
        item.Text = RequireText(text, "Item text");
        item.Section = NormalizeSection(section);
        item.Notes = (notes ?? string.Empty).Trim();
        item.Tags = NormalizeTags(tags);
        item.UpdatedAt = clock.Now;
    }

    public static bool DeleteItem(Checklist checklist, string itemId, IClock clock)
    {
        var removed = checklist.Items.RemoveAll(i => i.Id == itemId) > 0;
        if (removed)
        {
            NormalizeItemOrders(checklist.Items);
            checklist.UpdatedAt = clock.Now;
        }
        return removed;
    }

    /// <summary>
    /// Toggles completion. Completing stamps <see cref="ChecklistItem.CompletedAt"/>; un-completing
    /// clears it. This never touches <see cref="ChecklistItem.Order"/>, which is why an un-checked
    /// item drops straight back into its original position.
    /// </summary>
    public static void SetItemCompleted(ChecklistItem item, bool completed, IClock clock)
    {
        var now = clock.Now;
        item.Completed = completed;
        item.CompletedAt = completed ? now : null;
        item.UpdatedAt = now;
    }

    /// <summary>Changes an item's section (used by rename-section operations).</summary>
    public static void SetItemSection(ChecklistItem item, string? section, IClock clock)
    {
        item.Section = NormalizeSection(section);
        item.UpdatedAt = clock.Now;
    }

    // ---------------------------------------------------------------- ordering

    public static void NormalizeItemOrders(IList<ChecklistItem> items)
    {
        for (int i = 0; i < items.Count; i++)
            items[i].Order = i + 1;
    }

    public static void Move<T>(IList<T> list, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= list.Count) return;
        toIndex = Math.Clamp(toIndex, 0, list.Count - 1);
        if (fromIndex == toIndex) return;
        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(toIndex, item);
    }

    public static void ReorderItems(IList<ChecklistItem> items, int fromIndex, int toIndex)
    {
        Move(items, fromIndex, toIndex);
        NormalizeItemOrders(items);
    }

    /// <summary>
    /// Reorders one item to sit immediately before (or after) a target item that shares its section,
    /// keeping cross-section grouping stable. The whole list is renumbered afterward.
    /// </summary>
    public static void ReorderWithinSection(IList<ChecklistItem> items, ChecklistItem moved, ChecklistItem? target, bool placeAfter)
    {
        if (!items.Remove(moved))
            return;

        int insertAt;
        if (target is not null && items.Contains(target))
        {
            insertAt = items.IndexOf(target) + (placeAfter ? 1 : 0);
        }
        else
        {
            // No target: drop at the end of the moved item's section, before the next section starts.
            insertAt = LastIndexOfSection(items, NormalizeSection(moved.Section)) + 1;
            if (insertAt <= 0)
                insertAt = items.Count;
        }

        insertAt = Math.Clamp(insertAt, 0, items.Count);
        items.Insert(insertAt, moved);
        NormalizeItemOrders(items);
    }

    private static int LastIndexOfSection(IList<ChecklistItem> items, string section)
    {
        var last = -1;
        for (int i = 0; i < items.Count; i++)
            if (string.Equals(NormalizeSection(items[i].Section), section, StringComparison.Ordinal))
                last = i;
        return last;
    }

    // ---------------------------------------------------------------- views & grouping

    public static IEnumerable<ChecklistItem> OrderedItems(Checklist checklist) =>
        checklist.Items.OrderBy(i => i.Order);

    public static IEnumerable<ChecklistItem> OpenItems(Checklist checklist) =>
        checklist.Items.Where(i => !i.Completed).OrderBy(i => i.Order);

    public static IEnumerable<ChecklistItem> CompletedItems(Checklist checklist) =>
        checklist.Items.Where(i => i.Completed).OrderBy(i => i.Order);

    /// <summary>
    /// Groups items by section. Sections appear in the order their first item appears (by order),
    /// items within a section are sorted by order, and a blank section is reported as "General".
    /// </summary>
    public static IReadOnlyList<SectionItemGroup> GroupBySection(IEnumerable<ChecklistItem> items)
    {
        var groups = new List<SectionItemGroup>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var item in items.OrderBy(i => i.Order))
        {
            var section = NormalizeSection(item.Section);
            if (!index.TryGetValue(section, out var gi))
            {
                gi = groups.Count;
                index[section] = gi;
                groups.Add(new SectionItemGroup(section, new List<ChecklistItem>()));
            }
            ((List<ChecklistItem>)groups[gi].Items).Add(item);
        }

        return groups;
    }

    // ---------------------------------------------------------------- tags

    /// <summary>Parses a semicolon-separated tag string: trims, drops empties, removes case-insensitive duplicates.</summary>
    public static List<string> ParseTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();
        return NormalizeTags(input.Split(';'));
    }

    /// <summary>Cleans a tag collection: trims, drops empties, removes case-insensitive duplicates (keeping first casing).</summary>
    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        var result = new List<string>();
        if (tags is null)
            return result;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in tags)
        {
            var trimmed = (raw ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                continue;
            if (seen.Add(trimmed))
                result.Add(trimmed);
        }
        return result;
    }

    /// <summary>Formats tags as "a; b; c" for CSV export and the editor.</summary>
    public static string FormatTags(IEnumerable<string> tags) => string.Join("; ", tags);

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Returns <paramref name="desiredName"/> if unused, otherwise the first free
    /// "{name} Copy", "{name} Copy 2", "{name} Copy 3"... (case-insensitive).
    /// </summary>
    public static string MakeUniqueName(IEnumerable<string> existingNames, string desiredName)
    {
        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var baseName = (desiredName ?? string.Empty).Trim();
        if (baseName.Length == 0)
            baseName = "Checklist";

        if (!taken.Contains(baseName))
            return baseName;

        for (int i = 1; ; i++)
        {
            var candidate = i == 1 ? $"{baseName} Copy" : $"{baseName} Copy {i}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    public static string NormalizeSection(string? section) =>
        string.IsNullOrWhiteSpace(section) ? DefaultSection : section.Trim();

    private static string RequireText(string? value, string fieldName)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException($"{fieldName} cannot be blank.", nameof(value));
        return trimmed;
    }
}
