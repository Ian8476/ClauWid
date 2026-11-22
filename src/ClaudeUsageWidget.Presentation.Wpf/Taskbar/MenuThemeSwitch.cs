using System.Runtime.InteropServices;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Makes the process's native menus dark or light, to match the taskbar they open from.
/// Win32 menus ignore the Windows mode unless the process opts in, and the only way to opt in
/// is two undocumented exports of uxtheme.dll, found by ordinal: <c>SetPreferredAppMode</c>
/// (135) and <c>FlushMenuThemes</c> (136). They exist from Windows 10 1903 (build 18362) on;
/// before it, ordinal 135 is a different function, so older builds are left alone.
/// Nothing here is allowed to fail loudly: if the exports are not there, or Windows changes
/// them, the menu simply stays light, which is a cosmetic loss and nothing else.
/// </summary>
public static class MenuThemeSwitch
{
    private const string ThemeLibrary = "uxtheme.dll";
    private const int SetPreferredAppModeOrdinal = 135;
    private const int FlushMenuThemesOrdinal = 136;
    private const int FirstBuildWithTheExports = 18362;

    // PreferredAppMode: Default follows the app's own manifest, AllowDark follows the Windows
    // mode on its own, ForceDark and ForceLight ignore it. The taskbar has its own mode, a
    // separate setting from the one for apps, so the choice is forced instead of followed.
    private const int ForceDarkMode = 2;
    private const int ForceLightMode = 3;

    private static readonly Lazy<Exports?> Resolved = new(Resolve);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SetPreferredAppModeFunction(int mode);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void FlushMenuThemesFunction();

    /// <summary>Applies the mode to menus opened from now on. Does nothing where it is not supported.</summary>
    public static void Apply(bool light)
    {
        if (Resolved.Value is not { } exports)
        {
            return;
        }

        exports.SetPreferredAppMode(light ? ForceLightMode : ForceDarkMode);
        exports.FlushMenuThemes();
    }

    private static Exports? Resolve()
    {
        if (Environment.OSVersion.Version.Build < FirstBuildWithTheExports
            || !NativeLibrary.TryLoad(ThemeLibrary, out var library))
        {
            return null;
        }

        var setMode = GetProcAddress(library, new IntPtr(SetPreferredAppModeOrdinal));
        var flush = GetProcAddress(library, new IntPtr(FlushMenuThemesOrdinal));

        return setMode == IntPtr.Zero || flush == IntPtr.Zero
            ? null
            : new Exports(
                Marshal.GetDelegateForFunctionPointer<SetPreferredAppModeFunction>(setMode),
                Marshal.GetDelegateForFunctionPointer<FlushMenuThemesFunction>(flush));
    }

    private sealed record Exports(SetPreferredAppModeFunction SetPreferredAppMode, FlushMenuThemesFunction FlushMenuThemes);

    // Looking a function up by ordinal means passing the number where the name goes, which is
    // why the second parameter is a pointer-sized integer and not a string.
    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);
}
