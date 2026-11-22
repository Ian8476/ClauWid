using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// The widget's right-click menu, as a native Win32 popup menu. A WPF menu cannot be used
/// here: the widget is a child of the taskbar, so it can never be the foreground window, and
/// Explorer takes the foreground back from the WPF popup a moment after it opens, which closes it.
/// A native menu tracked from a hidden top-level window of our own, made the foreground window
/// first, is the classic notification-area pattern, and Explorer leaves it alone.
/// </summary>
public sealed class TaskbarMenu : IDisposable
{
    private const string OwnerName = "ClaudeUsageWidgetMenuOwner";
    private const string RefreshText = "Actualizar ahora";
    private const string HideText = "Ocultar";
    private const string ExitText = "Salir";

    // WS_POPUP: a bare top-level window. Not WS_VISIBLE, it is never shown.
    private const int PopupStyle = unchecked((int)0x80000000);

    // WS_EX_TOOLWINDOW: never in Alt+Tab or on the taskbar. Deliberately no WS_EX_NOACTIVATE:
    // the owner of a menu has to be able to become the foreground window.
    private const int ToolWindowStyle = 0x00000080;

    // AppendMenu flags: MF_STRING is a text item, MF_SEPARATOR a line between items.
    private const uint TextItem = 0x0000;
    private const uint SeparatorItem = 0x0800;

    // TrackPopupMenuEx flags. The command comes back as the return value instead of as a
    // message, and the menu opens above the point, the way it does from a taskbar at the bottom.
    private const uint ReturnCommand = 0x0100;
    private const uint TrackWithRightButton = 0x0002;
    private const uint AlignBottom = 0x0020;
    private const uint AlignLeft = 0x0000;
    private const uint AlignRight = 0x0008;

    // WM_NULL: a message that does nothing, see ShowAt.
    private const uint NullMessage = 0x0000;

    private readonly HwndSource _owner;
    private readonly IntPtr _menu;

    private bool _isShowing;
    private bool _isDisposed;

    public TaskbarMenu()
    {
        _owner = new HwndSource(new HwndSourceParameters(OwnerName)
        {
            WindowStyle = PopupStyle,
            ExtendedWindowStyle = ToolWindowStyle,
            PositionX = 0,
            PositionY = 0,
            Width = 0,
            Height = 0
        });

        _menu = CreatePopupMenu();
        if (_menu == IntPtr.Zero)
        {
            _owner.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        Append(TextItem, (UIntPtr)MenuCommand.Refresh, RefreshText);
        Append(SeparatorItem, UIntPtr.Zero, null);
        Append(TextItem, (UIntPtr)MenuCommand.Hide, HideText);
        Append(TextItem, (UIntPtr)MenuCommand.Exit, ExitText);
    }

    /// <summary>
    /// Opens the menu at a point of the screen, in device pixels, and waits for it to close.
    /// Returns what the user chose, or null if the menu was dismissed.
    /// </summary>
    public MenuCommand? ShowAt(Point screenPixels)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // Menus are themed as they open, so this has to come first for this one to match the taskbar.
        MenuThemeSwitch.Apply(SystemTaskbarTheme.IsLight());

        // A menu closes when the user clicks elsewhere only if its owner is the foreground
        // window. Without this it would stay open, or not take the keyboard.
        SetForegroundWindow(_owner.Handle);

        var alignment = SystemParameters.MenuDropAlignment ? AlignRight : AlignLeft;

        int chosen;
        _isShowing = true;
        try
        {
            chosen = TrackPopupMenuEx(
                _menu,
                ReturnCommand | TrackWithRightButton | AlignBottom | alignment,
                (int)Math.Round(screenPixels.X),
                (int)Math.Round(screenPixels.Y),
                _owner.Handle,
                IntPtr.Zero);
        }
        finally
        {
            _isShowing = false;
        }

        if (_isDisposed)
        {
            // The widget went away while the menu was open, and Dispose left the cleanup to here.
            Destroy();
            return null;
        }

        // Documented workaround (KB 135788): without a message reaching the owner after the
        // menu closes, the next menu opened from the same owner can close immediately.
        PostMessage(_owner.Handle, NullMessage, IntPtr.Zero, IntPtr.Zero);

        return chosen == 0 ? null : (MenuCommand)chosen;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Destroying a menu that is being tracked is not safe: ShowAt does it when it returns.
        if (!_isShowing)
        {
            Destroy();
        }
    }

    private void Destroy()
    {
        DestroyMenu(_menu);
        _owner.Dispose();
    }

    private void Append(uint flags, UIntPtr id, string? text)
    {
        if (!AppendMenu(_menu, flags, id, text))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
