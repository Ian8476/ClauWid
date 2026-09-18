using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeUsageWidget.Tests.Support;

/// <summary>
/// Delivers WM_WTSSESSION_CHANGE to this process's SystemEvents window: the same message
/// Windows sends on a real lock or unlock, without locking the machine running the tests.
/// </summary>
internal static class SessionEvents
{
    private const int SessionChangeMessage = 0x02B1;
    private const int SessionLock = 0x7;
    private const int SessionUnlock = 0x8;
    private const string BroadcastWindowClass = "BroadcastEventWindow";

    public static void Lock() => Post(SessionLock);

    public static void Unlock() => Post(SessionUnlock);

    private static void Post(int reason)
    {
        var window = FindSystemEventsWindow();
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException("This process has no SystemEvents window to notify.");
        }

        PostMessage(window, SessionChangeMessage, reason, Process.GetCurrentProcess().SessionId);
    }

    private static IntPtr FindSystemEventsWindow()
    {
        var processId = (uint)Environment.ProcessId;
        var found = IntPtr.Zero;

        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner != processId)
            {
                return true;
            }

            var className = new StringBuilder(256);
            GetClassName(window, className, className.Capacity);
            if (!className.ToString().Contains(BroadcastWindowClass, StringComparison.Ordinal))
            {
                return true;
            }

            found = window;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int capacity);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
