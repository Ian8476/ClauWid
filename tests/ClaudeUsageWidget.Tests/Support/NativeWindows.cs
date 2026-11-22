using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaudeUsageWidget.Presentation.Wpf.Controls;

namespace ClaudeUsageWidget.Tests.Support;

/// <summary>Reads back what Windows itself says about the windows the widget made.</summary>
internal static class NativeWindows
{
    // GWL_STYLE, and the WS_VISIBLE bit in it: the window's own flag. Unlike IsWindowVisible
    // it does not depend on the parent being shown, and the fake taskbar never is.
    private const int StyleIndex = -16;
    private const int VisibleStyle = 0x10000000;

    private const int ExtendedStyleIndex = -20;
    private const int ChildStyle = 0x40000000;

    public const int LayeredStyle = 0x00080000;
    public const int NoActivateStyle = 0x08000000;

    public const int MouseActivateNoActivate = 3;

    public const int WmContextMenu = 0x007B;

    private const uint WmMouseActivate = 0x0021;
    private const int WmLButtonDown = 0x0201;
    private const int HtClient = 1;

    // GW_CHILD and GW_HWNDNEXT.
    private const uint FirstChild = 5;
    private const uint NextSibling = 2;

    /// <summary>The native windows hosting a widget view that are alive on this thread.</summary>
    public static List<IntPtr> WidgetWindows() =>
        PresentationSource.CurrentSources
            .OfType<HwndSource>()
            .Where(source => source.RootVisual is TaskbarWidgetView)
            .Select(source => source.Handle)
            .ToList();

    public static IntPtr ParentOf(IntPtr window) => GetParent(window);

    public static bool IsShown(IntPtr window) => (GetWindowLong(window, StyleIndex) & VisibleStyle) != 0;

    public static bool IsChild(IntPtr window) => (GetWindowLong(window, StyleIndex) & ChildStyle) != 0;

    public static bool HasExtendedStyle(IntPtr window, int style) =>
        (GetWindowLong(window, ExtendedStyleIndex) & style) != 0;

    /// <summary>
    /// Asks the window what it wants done about a left click on its client area, the way
    /// Windows does before activating it. Returns MA_ACTIVATE (1), MA_NOACTIVATE (3) and so on.
    /// </summary>
    public static int AskHowToHandleLeftClick(IntPtr window, IntPtr topLevelParent)
    {
        // The low word of lParam is the hit-test code, the high word the mouse message.
        var lParam = new IntPtr((WmLButtonDown << 16) | HtClient);
        return (int)SendMessage(window, WmMouseActivate, topLevelParent, lParam);
    }

    /// <summary>Sends the window the message that asks for its context menu, and returns its answer.</summary>
    public static IntPtr SendContextMenu(IntPtr window) =>
        SendMessage(window, WmContextMenu, window, IntPtr.Zero);

    /// <summary>The rectangle of the window in screen pixels.</summary>
    public static Int32Rect ScreenBounds(IntPtr window)
    {
        Assert.True(GetWindowRect(window, out var bounds));
        return new Int32Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
    }

    /// <summary>The children of a window from the topmost down.</summary>
    public static List<IntPtr> ChildrenTopDown(IntPtr parent)
    {
        var children = new List<IntPtr>();

        for (var child = GetWindow(parent, FirstChild); child != IntPtr.Zero; child = GetWindow(child, NextSibling))
        {
            children.Add(child);
        }

        return children;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint relation);
}
