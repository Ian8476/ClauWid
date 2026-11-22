using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// The rectangle the widget takes inside the taskbar, in device pixels, and the zoom its
/// content needs to fill it. The widget is designed for a taskbar of a given thickness and is
/// scaled as a whole when the real one is thicker or thinner.
/// </summary>
public readonly record struct TaskbarSlot(Int32Rect Bounds, double ContentScale)
{
    /// <param name="taskbar">Taskbar and the occupied space at its end, in device pixels.</param>
    /// <param name="naturalSize">Size of the content at zoom 1, in device-independent units.</param>
    /// <param name="dpiScale">Device pixels per device-independent unit.</param>
    public static TaskbarSlot For(TaskbarGeometry taskbar, Size naturalSize, double dpiScale)
    {
        var bounds = taskbar.Bounds;
        var occupied = taskbar.OccupiedBounds;

        return bounds.Width >= bounds.Height
            ? ForHorizontal(bounds, occupied, naturalSize, dpiScale)
            : ForVertical(bounds, occupied, naturalSize, dpiScale);
    }

    /// <summary>
    /// Full taskbar height, glued to the inner edge of the occupied space: to its left
    /// normally, to its right on right-to-left systems, where the taskbar is mirrored.
    /// </summary>
    private static TaskbarSlot ForHorizontal(Int32Rect bounds, Int32Rect occupied, Size naturalSize, double dpiScale)
    {
        var scale = bounds.Height / (naturalSize.Height * dpiScale);
        var width = (int)Math.Round(naturalSize.Width * dpiScale * scale);

        var occupiedIsAtTheEnd = occupied.X + occupied.Width / 2 >= bounds.X + bounds.Width / 2;
        var left = occupiedIsAtTheEnd ? occupied.X - width : occupied.X + occupied.Width;

        return new TaskbarSlot(new Int32Rect(left, bounds.Y, width, bounds.Height), scale);
    }

    /// <summary>A taskbar docked to a side of the screen: full width, right above the occupied space.</summary>
    private static TaskbarSlot ForVertical(Int32Rect bounds, Int32Rect occupied, Size naturalSize, double dpiScale)
    {
        var scale = bounds.Width / (naturalSize.Width * dpiScale);
        var height = (int)Math.Round(naturalSize.Height * dpiScale * scale);

        return new TaskbarSlot(new Int32Rect(bounds.X, occupied.Y - height, bounds.Width, height), scale);
    }
}
