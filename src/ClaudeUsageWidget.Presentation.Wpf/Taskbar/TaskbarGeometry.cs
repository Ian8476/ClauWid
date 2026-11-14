using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Where the taskbar and its notification area (chevron, system icons, clock) are right now,
/// in device pixels. <see cref="Handle"/> is the taskbar window the widget docks to.
/// </summary>
public sealed record TaskbarGeometry(IntPtr Handle, IntPtr TrayHandle, Int32Rect Bounds, Int32Rect TrayBounds);
