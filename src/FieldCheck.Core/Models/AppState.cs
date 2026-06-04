namespace FieldCheck.Core.Models;

/// <summary>
/// The root persisted document. <see cref="Version"/> lets me detect older files (e.g. v1, which
/// stored checklists at the root) and migrate them instead of discarding user data.
/// </summary>
public sealed class AppState
{
    /// <summary>
    /// Current schema version.
    /// v1 = checklists at the root (with a per-checklist projectName).
    /// v2 = first-class projects containing checklists; items gained a tags list.
    /// v3 = items gained a four-state Status (Open/Complete/Issue/N/A) and an issue note; the
    ///      legacy boolean completed/completedAt are preserved and kept in sync with Status.
    /// </summary>
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public AppSettings Settings { get; set; } = new();

    public List<Project> Projects { get; set; } = new();
}
