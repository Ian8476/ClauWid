using System.Runtime.InteropServices;
using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Finds the primary taskbar by its window class. On Windows 11 the taskbar is drawn by a single
/// XAML surface, but the classic notification area window is still there and still sized to
/// match what is drawn, so its rectangle says where the free space ends. Widgets that other
/// apps dock against it, as child windows of the taskbar, push that edge further in.
/// </summary>
public sealed class Win32TaskbarLocator : ITaskbarLocator
{
    private const string TaskbarClass = "Shell_TrayWnd";
    private const string TrayClass = "TrayNotifyWnd";

    // GW_CHILD: the topmost child of a window. GW_HWNDNEXT: the sibling right below a window.
    private const uint FirstChild = 5;
    private const uint NextSibling = 2;

    // EVENT_OBJECT_REORDER: a window changed the stacking order of its children. The window
    // reporting it is the parent whose children moved.
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

        var taskbarBounds = bounds.ToInt32Rect();
        var occupied = TaskbarOccupiedArea.Chain(taskbarBounds, trayBounds, ForeignWidgetBounds(taskbar));

        return new TaskbarGeometry(taskbar, tray, taskbarBounds, occupied);
    }

    /// <summary>
    /// Three things count as a change: the taskbar or its notification area moving or
    /// resizing, and the taskbar's own children being reordered, which is what Explorer does
    /// when it raises its content above the widget. Going behind a full-screen app is not
    /// among them: the widget is a child of the taskbar, so it goes along by itself.
    /// Hooked on the taskbar's own thread only: Explorer's file windows live on other threads
    /// of the same process, and dragging one of them around would otherwise flood this one.
    /// The two events are hooked apart because everything numbered between them (focus,
    /// selection, state changes) fires constantly while the mouse is over the taskbar.
    /// Widgets of other apps appearing, vanishing or resizing happen in their own threads and
    /// processes, so no hook here sees them: the host looks again on a timer for those.
    /// </summary>
    public IDisposable Watch(TaskbarGeometry taskbar, Action onChanged)
    {
        var thread = GetWindowThreadProcessId(taskbar.Handle, out var process);

        WinEventCallback callback = (_, eventType, window, objectId, _, _, _) =>
        {
            var changed = eventType == ReorderEvent
                ? window == taskbar.Handle
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

    /// <summary>
    /// The visible direct children of the taskbar that are neither Explorer's own nor ours:
    /// the widgets other apps dock there. Only direct children matter, what a widget holds
    /// inside itself does not take space of the taskbar.
    /// </summary>
    private static List<Int32Rect> ForeignWidgetBounds(IntPtr taskbar)
    {
        GetWindowThreadProcessId(taskbar, out var explorerProcess);
        var ownProcess = (uint)Environment.ProcessId;
        var widgets = new List<Int32Rect>();

        for (var child = GetWindow(taskbar, FirstChild); child != IntPtr.Zero; child = GetWindow(child, NextSibling))
        {
            GetWindowThreadProcessId(child, out var process);

            if (process != explorerProcess
                && process != ownProcess
                && IsWindowVisible(child)
                && GetWindowRect(child, out var bounds))
            {
                widgets.Add(bounds.ToInt32Rect());
            }
        }

        return widgets;
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
    private static extern IntPtr GetWindow(IntPtr window, uint relation);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

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
