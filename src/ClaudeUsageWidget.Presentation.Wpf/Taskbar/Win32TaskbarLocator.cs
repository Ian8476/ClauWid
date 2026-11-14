using System.Runtime.InteropServices;
using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Finds the primary taskbar by its window class. On Windows 11 the taskbar is drawn by a single
/// XAML surface, but the classic notification area window is still there and still sized to
/// match what is drawn, so its rectangle says where the free space ends.
/// </summary>
public sealed class Win32TaskbarLocator : ITaskbarLocator
{
    private const string TaskbarClass = "Shell_TrayWnd";
    private const string TrayClass = "TrayNotifyWnd";

    // EVENT_OBJECT_REORDER: a window changed the stacking order of its children. For top-level
    // windows such as the taskbar, the window reporting it is the desktop.
    private const uint ReorderEvent = 0x8004;

    // EVENT_OBJECT_LOCATIONCHANGE: a window moved or resized.
    private const uint LocationChangeEvent = 0x800B;

    // OBJID_WINDOW: the event is about the window itself, not its caret, cursor or scroll bars.
    private const int WindowObjectId = 0;

    // WINEVENT_OUTOFCONTEXT: the callback runs here, through this thread's message loop.
    private const uint OutOfContext = 0;

    private delegate void WinEventCallback(
        IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    public TaskbarGeometry? Locate()
    {
        var taskbar = FindWindow(TaskbarClass, null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var bounds))
        {
            return null;
        }

        var tray = FindWindowEx(taskbar, IntPtr.Zero, TrayClass, null);

        // Without a notification area the free space runs to the far end of the taskbar.
        var trayBounds = tray != IntPtr.Zero && GetWindowRect(tray, out var found)
            ? found.ToInt32Rect()
            : new Int32Rect(bounds.Right, bounds.Bottom, 0, 0);

        return new TaskbarGeometry(taskbar, tray, bounds.ToInt32Rect(), trayBounds);
    }

    /// <summary>
    /// Two things count as a change: the taskbar or its notification area moving or resizing,
    /// and the taskbar going up or down in the stacking order, which is what it does to step
    /// behind a full-screen app and to come back afterwards.
    /// Hooked on the taskbar's own thread only: Explorer's file windows live on other threads
    /// of the same process, and dragging one of them around would otherwise flood this one.
    /// The two events are hooked apart because everything numbered between them (focus,
    /// selection, state changes) fires constantly while the mouse is over the taskbar.
    /// </summary>
    public IDisposable Watch(TaskbarGeometry taskbar, Action onChanged)
    {
        var thread = GetWindowThreadProcessId(taskbar.Handle, out var process);
        var desktop = GetDesktopWindow();

        WinEventCallback callback = (_, eventType, window, objectId, _, _, _) =>
        {
            var changed = eventType == ReorderEvent
                ? window == desktop
                : objectId == WindowObjectId && (window == taskbar.Handle || window == taskbar.TrayHandle);

            if (changed)
            {
                onChanged();
            }
        };

        return new Subscription(
            callback,
            SetWinEventHook(ReorderEvent, ReorderEvent, IntPtr.Zero, callback, process, thread, OutOfContext),
            SetWinEventHook(LocationChangeEvent, LocationChangeEvent, IntPtr.Zero, callback, process, thread, OutOfContext));
    }

    /// <summary>Holds the callback for as long as the hooks live: Windows only keeps a raw pointer to it.</summary>
    private sealed class Subscription : IDisposable
    {
        private readonly WinEventCallback _callback;
        private readonly IntPtr[] _hooks;
        private bool _isDisposed;

        public Subscription(WinEventCallback callback, params IntPtr[] hooks)
        {
            _callback = callback;
            _hooks = hooks;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            foreach (var hook in _hooks)
            {
                if (hook != IntPtr.Zero)
                {
                    UnhookWinEvent(hook);
                }
            }

            GC.KeepAlive(_callback);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
    private static extern IntPtr FindWindow(string className, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint firstEvent,
        uint lastEvent,
        IntPtr module,
        WinEventCallback callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Int32Rect ToInt32Rect() => new(Left, Top, Right - Left, Bottom - Top);
    }
}
