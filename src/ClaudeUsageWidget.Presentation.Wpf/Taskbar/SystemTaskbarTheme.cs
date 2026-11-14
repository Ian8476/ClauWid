using Microsoft.Win32;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// The taskbar follows the Windows mode, which is a separate setting from the app mode: a
/// light taskbar under dark apps, or the other way around, is a valid combination.
/// </summary>
public static class SystemTaskbarTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string SystemUsesLightThemeValue = "SystemUsesLightTheme";
    private const int LightThemeFlag = 1;

    /// <summary>Dark when the setting is missing, as on Windows versions that predate it.</summary>
    public static bool IsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(SystemUsesLightThemeValue) is LightThemeFlag;
    }
}
