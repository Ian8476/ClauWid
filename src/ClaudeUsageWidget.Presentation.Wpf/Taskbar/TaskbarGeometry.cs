using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Where the taskbar and the space already taken at its end are right now, in device pixels.
/// <see cref="Handle"/> is the taskbar window the widget docks to. <see cref="OccupiedBounds"/>
/// is the notification area (chevron, system icons, clock) plus other apps' widgets docked
/// against it: the widget goes right next to that, on the side facing the free space.
/// <see cref="TrayHandle"/> is the notification area window alone, which is what the
/// location hook follows.
/// </summary>
public sealed record TaskbarGeometry(IntPtr Handle, IntPtr TrayHandle, Int32Rect Bounds, Int32Rect OccupiedBounds);
