using System.IO;
using System.Windows;
using Microsoft.Win32;
using Stardust.Paradox.GremlinStudio.Core.Storage;

namespace Stardust.Paradox.GremlinStudio.Services;

/// <summary>
/// Service for managing application themes.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme mode.
    /// </summary>
    ThemeMode CurrentMode { get; }
    
    /// <summary>
    /// Gets whether the current effective theme is dark.
    /// </summary>
    bool IsDarkTheme { get; }
    
    /// <summary>
    /// Sets the theme mode.
    /// </summary>
    /// <param name="mode">The theme mode to apply.</param>
    void SetTheme(ThemeMode mode);
    
    /// <summary>
    /// Detects the system theme preference.
    /// </summary>
    /// <returns>True if the system prefers dark theme.</returns>
    bool DetectSystemDarkMode();
    
    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    event EventHandler<ThemeMode>? ThemeChanged;
}

/// <summary>
/// Implementation of theme management service.
/// </summary>
public class ThemeService : IThemeService
{
    private const string ThemePreferenceKey = "GremlinStudio_ThemeMode";
    private const string LightThemeUri = "Themes/LightTheme.xaml";
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string MoonLightThemeUri = "Themes/MoonLightTheme.xaml";
    private const string DarkForrestThemeUri = "Themes/DarkForrestTheme.xaml";
    private const string MuddyRiverThemeUri = "Themes/MuddyRiverTheme.xaml";
    
    private ThemeMode _currentMode = ThemeMode.System;
    private ResourceDictionary? _currentThemeDictionary;
    
    public ThemeMode CurrentMode => _currentMode;
    
    public bool IsDarkTheme => _currentMode == ThemeMode.Dark || 
        (_currentMode == ThemeMode.System && DetectSystemDarkMode());
    
    public event EventHandler<ThemeMode>? ThemeChanged;
    
    public ThemeService()
    {
        // Listen for system theme changes
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }
    
    public void SetTheme(ThemeMode mode)
    {
        _currentMode = mode;
        ApplyTheme();
        SavePreference(mode);
        ThemeChanged?.Invoke(this, mode);
    }
    
    public bool DetectSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0; // 0 = dark, 1 = light
            }
        }
        catch
        {
            // Fallback to light theme if detection fails
        }
        
        return false;
    }
    
    /// <summary>
    /// Initializes the theme from saved preference or system default.
    /// </summary>
    public void Initialize()
    {
        _currentMode = LoadPreference();
        ApplyTheme();
    }
    
    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app is null) return;

        var themeUri = _currentMode switch
        {
            ThemeMode.MoonLight => MoonLightThemeUri,
            ThemeMode.DarkForrest => DarkForrestThemeUri,
            ThemeMode.MuddyRiver => MuddyRiverThemeUri,
            ThemeMode.Dark => DarkThemeUri,
            ThemeMode.Light => LightThemeUri,
            ThemeMode.System => DetectSystemDarkMode() ? DarkThemeUri : LightThemeUri,
            _ => LightThemeUri
        };
        
        // Remove old theme dictionary if present
        if (_currentThemeDictionary is not null)
        {
            app.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
        }
        
        // Load and apply new theme
        _currentThemeDictionary = new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.Relative)
        };
        
        app.Resources.MergedDictionaries.Add(_currentThemeDictionary);
    }
    
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentMode == ThemeMode.System)
        {
            // Re-apply theme when system preference changes
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
                ThemeChanged?.Invoke(this, _currentMode);
            });
        }
    }
    
    private static void SavePreference(ThemeMode mode)
    {
        try
        {
            File.WriteAllText(AppDataPaths.ThemePreferenceFilePath, mode.ToString());
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    private static ThemeMode LoadPreference()
    {
        try
        {
            if (File.Exists(AppDataPaths.ThemePreferenceFilePath))
            {
                var modeStr = File.ReadAllText(AppDataPaths.ThemePreferenceFilePath).Trim();
                if (Enum.TryParse<ThemeMode>(modeStr, out var mode))
                {
                    return mode;
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
        
        return ThemeMode.System;
    }
}
