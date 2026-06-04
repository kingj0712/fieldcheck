namespace FieldCheck.Core.Models;

/// <summary>
/// Everything I need to restore the previous session: theme, last selection, and window
/// geometry. These all live under the top-level <c>settings</c> node in data.json.
/// </summary>
public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public string? LastOpenedChecklistId { get; set; }

    /// <summary>"open" or "completed".</summary>
    public string LastSelectedTab { get; set; } = "open";

    public double WindowWidth { get; set; } = 1200;

    public double WindowHeight { get; set; } = 800;

    /// <summary>Null until the user has moved the window at least once.</summary>
    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public bool WindowMaximized { get; set; }

    /// <summary>Sidebar width in pixels (clamped to a sane range by the UI).</summary>
    public double SidebarWidth { get; set; } = 300;

    /// <summary>Whether the left sidebar is collapsed.</summary>
    public bool SidebarCollapsed { get; set; }
}
