namespace FieldCheck.Core.Models;

/// <summary>
/// The three theme choices I persist in settings. I serialize these as the lowercase
/// strings "system" / "light" / "dark" to match the documented data model.
/// </summary>
public enum ThemeMode
{
    System,
    Light,
    Dark
}
