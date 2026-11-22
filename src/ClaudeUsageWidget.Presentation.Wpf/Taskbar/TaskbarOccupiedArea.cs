using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// The pure rule behind <see cref="TaskbarGeometry.OccupiedBounds"/>. Other apps dock their
/// own widgets against the notification area, and those must not be covered. Starting from the
/// notification area, a foreign window joins the occupied space when it touches the edge that
/// faces the free space, and the next one is looked for from the new edge, so any number of
/// widgets side by side are crossed. The chain only ever starts at the notification area:
/// a window that docks against the edge of the occupied space on the far side of ours is
/// not part of it, so the widget never follows another app away from where it was.
/// </summary>
public static class TaskbarOccupiedArea
{
    /// <summary>
    /// How far apart, in device pixels, two windows may be and still count as touching.
    /// Apps round their positions and add a pixel or two of margin.
    /// </summary>
    public const int TouchTolerance = 4;

    /// <param name="taskbar">The taskbar, in device pixels.</param>
    /// <param name="tray">The notification area, in the same pixels.</param>
    /// <param name="foreign">
    /// Visible windows of other apps inside the taskbar, in the same pixels. The caller leaves
    /// out Explorer's own windows and this app's.
    /// </param>
    public static Int32Rect Chain(Int32Rect taskbar, Int32Rect tray, IReadOnlyList<Int32Rect> foreign)
    {
        var edge = EdgeFacingTheFreeSpace(taskbar, tray);
        var occupied = tray;
        var pending = foreign.ToList();

        int next;
        while ((next = pending.FindIndex(candidate => Touches(edge, taskbar, occupied, candidate))) >= 0)
        {
            occupied = Union(occupied, pending[next]);
            pending.RemoveAt(next);
        }

        return occupied;
    }

    private enum Edge
    {
        Left,
        Right,
        Top
    }

    /// <summary>
    /// The side of the occupied space that faces the free space: its left on a normal
    /// horizontal taskbar, its right when the taskbar is mirrored (notification area at the
    /// start), and its top on a vertical one. Decided the same way <see cref="TaskbarSlot"/> does.
    /// </summary>
    private static Edge EdgeFacingTheFreeSpace(Int32Rect taskbar, Int32Rect tray)
    {
        if (taskbar.Width < taskbar.Height)
        {
            return Edge.Top;
        }

        var trayIsAtTheEnd = tray.X + tray.Width / 2 >= taskbar.X + taskbar.Width / 2;
        return trayIsAtTheEnd ? Edge.Left : Edge.Right;
    }

    /// <summary>
    /// Touching the edge from the free-space side, and sharing part of the taskbar's thickness
    /// with it: a window that is only near the edge in one axis is not docked there.
    /// </summary>
    private static bool Touches(Edge edge, Int32Rect taskbar, Int32Rect occupied, Int32Rect candidate)
    {
        if (candidate.Width <= 0 || candidate.Height <= 0)
        {
            return false;
        }

        return edge switch
        {
            Edge.Left => Math.Abs(candidate.X + candidate.Width - occupied.X) <= TouchTolerance
                && OverlapsVertically(taskbar, candidate),
            Edge.Right => Math.Abs(candidate.X - (occupied.X + occupied.Width)) <= TouchTolerance
                && OverlapsVertically(taskbar, candidate),
            _ => Math.Abs(candidate.Y + candidate.Height - occupied.Y) <= TouchTolerance
                && OverlapsHorizontally(taskbar, candidate)
        };
    }

    private static bool OverlapsVertically(Int32Rect taskbar, Int32Rect candidate) =>
        candidate.Y < taskbar.Y + taskbar.Height && candidate.Y + candidate.Height > taskbar.Y;

    private static bool OverlapsHorizontally(Int32Rect taskbar, Int32Rect candidate) =>
        candidate.X < taskbar.X + taskbar.Width && candidate.X + candidate.Width > taskbar.X;

    private static Int32Rect Union(Int32Rect first, Int32Rect second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);

        return new Int32Rect(left, top, right - left, bottom - top);
    }
}
