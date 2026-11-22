using System.Windows;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarSlotTests
{
    private static readonly Size NaturalSize = new(198, 48);

    [Fact]
    public void Sits_right_before_the_notification_area_at_full_taskbar_height()
    {
        // A 1920x1080 screen at 125%: the taskbar is 48 units thick, 60 pixels.
        var taskbar = Taskbar(new Int32Rect(0, 1020, 1920, 60), new Int32Rect(1524, 1020, 396, 60));

        var slot = TaskbarSlot.For(taskbar, NaturalSize, dpiScale: 1.25);

        Assert.Equal(new Int32Rect(1276, 1020, 248, 60), slot.Bounds);
        Assert.Equal(1d, slot.ContentScale, precision: 6);
    }

    [Fact]
    public void Scales_with_a_thicker_taskbar()
    {
        var taskbar = Taskbar(new Int32Rect(0, 1008, 1920, 72), new Int32Rect(1600, 1008, 320, 72));

        var slot = TaskbarSlot.For(taskbar, NaturalSize, dpiScale: 1d);

        Assert.Equal(new Int32Rect(1303, 1008, 297, 72), slot.Bounds);
        Assert.Equal(1.5d, slot.ContentScale, precision: 6);
    }

    [Fact]
    public void Goes_after_the_notification_area_on_a_mirrored_taskbar()
    {
        var taskbar = Taskbar(new Int32Rect(0, 1032, 1920, 48), new Int32Rect(0, 1032, 300, 48));

        var slot = TaskbarSlot.For(taskbar, NaturalSize, dpiScale: 1d);

        Assert.Equal(new Int32Rect(300, 1032, 198, 48), slot.Bounds);
    }

    [Fact]
    public void Takes_the_far_end_when_there_is_no_notification_area()
    {
        // What the locator reports when it finds no notification area: empty, at the far corner.
        var taskbar = Taskbar(new Int32Rect(0, 1032, 1920, 48), new Int32Rect(1920, 1080, 0, 0));

        var slot = TaskbarSlot.For(taskbar, NaturalSize, dpiScale: 1d);

        Assert.Equal(new Int32Rect(1722, 1032, 198, 48), slot.Bounds);
    }

    [Fact]
    public void Shrinks_to_the_width_of_a_vertical_taskbar_above_its_notification_area()
    {
        var taskbar = Taskbar(new Int32Rect(0, 0, 99, 1080), new Int32Rect(0, 900, 99, 180));

        var slot = TaskbarSlot.For(taskbar, NaturalSize, dpiScale: 1d);

        Assert.Equal(new Int32Rect(0, 876, 99, 24), slot.Bounds);
        Assert.Equal(0.5d, slot.ContentScale, precision: 6);
    }

    private static TaskbarGeometry Taskbar(Int32Rect bounds, Int32Rect occupiedBounds) =>
        new(new IntPtr(1), new IntPtr(2), bounds, occupiedBounds);
}
