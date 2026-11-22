using System.Windows;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarOccupiedAreaTests
{
    // A 1920x1080 screen with a 48 px taskbar and a 300 px notification area at its end.
    private static readonly Int32Rect Taskbar = new(0, 1032, 1920, 48);
    private static readonly Int32Rect Tray = new(1620, 1032, 300, 48);

    [Fact]
    public void Is_the_notification_area_when_nobody_else_docked()
    {
        Assert.Equal(Tray, TaskbarOccupiedArea.Chain(Taskbar, Tray, []));
    }

    [Fact]
    public void Includes_a_widget_docked_against_the_notification_area()
    {
        var widget = new Int32Rect(1500, 1032, 120, 48);

        var occupied = TaskbarOccupiedArea.Chain(Taskbar, Tray, [widget]);

        Assert.Equal(new Int32Rect(1500, 1032, 420, 48), occupied);
    }

    [Fact]
    public void Includes_a_widget_a_few_pixels_away_from_the_notification_area()
    {
        var widget = new Int32Rect(1500, 1032, 120 - TaskbarOccupiedArea.TouchTolerance, 48);

        var occupied = TaskbarOccupiedArea.Chain(Taskbar, Tray, [widget]);

        Assert.Equal(widget.X, occupied.X);
    }

    [Fact]
    public void Chains_two_adjacent_widgets_whatever_the_order_they_are_listed_in()
    {
        var near = new Int32Rect(1500, 1032, 120, 48);
        var far = new Int32Rect(1380, 1032, 120, 48);

        var occupied = TaskbarOccupiedArea.Chain(Taskbar, Tray, [far, near]);

        Assert.Equal(new Int32Rect(1380, 1032, 540, 48), occupied);
    }

    [Fact]
    public void Ignores_a_window_that_does_not_touch_the_occupied_edge()
    {
        var apart = new Int32Rect(1000, 1032, 120, 48);

        Assert.Equal(Tray, TaskbarOccupiedArea.Chain(Taskbar, Tray, [apart]));
    }

    [Fact]
    public void Stops_at_the_first_gap()
    {
        var near = new Int32Rect(1500, 1032, 120, 48);
        var beyondGap = new Int32Rect(1000, 1032, 120, 48);

        var occupied = TaskbarOccupiedArea.Chain(Taskbar, Tray, [near, beyondGap]);

        Assert.Equal(1500, occupied.X);
    }

    [Fact]
    public void Ignores_a_window_that_does_not_overlap_the_thickness_of_the_taskbar()
    {
        var above = new Int32Rect(1500, 900, 120, 48);

        Assert.Equal(Tray, TaskbarOccupiedArea.Chain(Taskbar, Tray, [above]));
    }

    [Fact]
    public void Ignores_a_window_without_an_area()
    {
        var empty = new Int32Rect(1620, 1032, 0, 0);

        Assert.Equal(Tray, TaskbarOccupiedArea.Chain(Taskbar, Tray, [empty]));
    }

    [Fact]
    public void Ignores_a_window_on_the_far_side_of_the_notification_area()
    {
        // Past the end of the taskbar the free space is on the other side: nothing to chain to.
        var behind = new Int32Rect(1920, 1032, 120, 48);

        Assert.Equal(Tray, TaskbarOccupiedArea.Chain(Taskbar, Tray, [behind]));
    }

    [Fact]
    public void Chains_toward_the_end_of_a_mirrored_taskbar()
    {
        // Right-to-left: the notification area is at the start and the free space is after it.
        var tray = new Int32Rect(0, 1032, 300, 48);
        var widget = new Int32Rect(300, 1032, 120, 48);
        var apart = new Int32Rect(600, 1032, 120, 48);

        var occupied = TaskbarOccupiedArea.Chain(Taskbar, tray, [widget, apart]);

        Assert.Equal(new Int32Rect(0, 1032, 420, 48), occupied);
    }

    [Fact]
    public void Chains_upward_on_a_vertical_taskbar()
    {
        var taskbar = new Int32Rect(0, 0, 99, 1080);
        var tray = new Int32Rect(0, 900, 99, 180);
        var widget = new Int32Rect(0, 840, 99, 60);
        var apart = new Int32Rect(0, 600, 99, 60);

        var occupied = TaskbarOccupiedArea.Chain(taskbar, tray, [apart, widget]);

        Assert.Equal(new Int32Rect(0, 840, 99, 240), occupied);
    }

    [Fact]
    public void Ignores_a_window_outside_the_width_of_a_vertical_taskbar()
    {
        var taskbar = new Int32Rect(0, 0, 99, 1080);
        var tray = new Int32Rect(0, 900, 99, 180);
        var beside = new Int32Rect(200, 840, 99, 60);

        Assert.Equal(tray, TaskbarOccupiedArea.Chain(taskbar, tray, [beside]));
    }
}
