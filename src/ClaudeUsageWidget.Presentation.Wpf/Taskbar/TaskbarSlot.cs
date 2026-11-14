using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// The rectangle the widget takes inside the taskbar, in device pixels, and the zoom its
/// content needs to fill it. The widget is designed for a taskbar of a given thickness and is
/// scaled as a whole when the real one is thicker or thinner.
/// </summary>
public readonly record struct TaskbarSlot(Int32Rect Bounds, double ContentScale)
{
    /// <param name="taskbar">Taskbar and notification area, in device pixels.</param>
    /// <param name="naturalSize">Size of the content at zoom 1, in device-independent units.</param>
    /// <param name="dpiScale">Device pixels per device-independent unit.</param>
    public static TaskbarSlot For(TaskbarGeometry taskbar, Size naturalSize, double dpiScale)
    {
        var bounds = taskbar.Bounds;
        var tray = taskbar.TrayBounds;

        return bounds.Width >= bounds.Height
            ? ForHorizontal(bounds, tray, naturalSize, dpiScale)
            : ForVertical(bounds, tray, naturalSize, dpiScale);
    }

    /// <summary>
    /// Full taskbar height, glued to the inner edge of the notification area: to its left
    /// normally, to its right on right-to-left systems, where the taskbar is mirrored.
    /// </summary>
    private static TaskbarSlot ForHorizontal(Int32Rect bounds, Int32Rect tray, Size naturalSize, double dpiScale)
    {
        var scale = bounds.Height / (naturalSize.Height * dpiScale);
        var width = (int)Math.Round(naturalSize.Width * dpiScale * scale);

        var trayIsAtTheEnd = tray.X + tray.Width / 2 >= bounds.X + bounds.Width / 2;
        var left = trayIsAtTheEnd ? tray.X - width : tray.X + tray.Width;

        return new TaskbarSlot(new Int32Rect(left, bounds.Y, width, bounds.Height), scale);
    }

    /// <summary>A taskbar docked to a side of the screen: full width, right above the notification area.</summary>
    private static TaskbarSlot ForVertical(Int32Rect bounds, Int32Rect tray, Size naturalSize, double dpiScale)
    {
        var scale = bounds.Width / (naturalSize.Width * dpiScale);
        var height = (int)Math.Round(naturalSize.Height * dpiScale * scale);

        return new TaskbarSlot(new Int32Rect(bounds.X, tray.Y - height, bounds.Width, height), scale);
    }
}
