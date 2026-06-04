using System.Windows;
using Microsoft.Win32;
using FieldCheck.Core.Models;

namespace FieldCheck.App.Services;

/// <summary>
/// Applies the Light or Dark resource dictionary at runtime and resolves "System" by reading
/// the Windows apps-theme preference. The active dictionary is swapped in place so every
/// DynamicResource brush updates instantly.
/// </summary>
public sealed class ThemeService
{
    private const string PackPrefix = "pack://application:,,,/FieldCheck;component/Themes/";
    private ResourceDictionary? _activeThemeDictionary;

    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    /// <summary>The theme actually displayed (System resolves to Light or Dark).</summary>
    public ThemeMode EffectiveMode { get; private set; } = ThemeMode.Light;

    public event EventHandler? ThemeChanged;

    public void Apply(ThemeMode mode)
    {
        CurrentMode = mode;
        EffectiveMode = Resolve(mode);

        var uri = new Uri(PackPrefix + (EffectiveMode == ThemeMode.Dark ? "Dark.xaml" : "Light.xaml"), UriKind.Absolute);
        var dictionary = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_activeThemeDictionary is not null)
            merged.Remove(_activeThemeDictionary);
        // Insert the theme first so shared control styles (added after) can override if needed.
        merged.Insert(0, dictionary);
        _activeThemeDictionary = dictionary;

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ThemeMode Resolve(ThemeMode mode) =>
        mode == ThemeMode.System ? (IsSystemDark() ? ThemeMode.Dark : ThemeMode.Light) : mode;

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int appsUseLight)
                return appsUseLight == 0;
        }
        catch
        {
            // If the registry is unavailable for any reason, fall back to light.
        }
        return false;
    }
}
