using System.Windows;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarWidgetHostTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(200);

    // Past the host's look at the taskbar every two seconds.
    private static readonly TimeSpan PeriodicLook = TimeSpan.FromMilliseconds(2500);

    [Fact]
    public void Docks_before_the_occupied_area_as_a_child_of_the_taskbar() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);

        host.Start();
        Sta.Pump(Settle);

        var window = Assert.Single(NativeWindows.WidgetWindows());
        Assert.True(NativeWindows.IsShown(window));
        Assert.Equal(taskbar.Handle, NativeWindows.ParentOf(window));
        AssertFillsTheSlotBefore(taskbar.Geometry, window);
    });

    [Fact]
    public void Docks_before_the_other_widgets_that_the_occupied_area_includes() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);

        // Another app docks a 120 px widget against the notification area.
        var tray = taskbar.Geometry.OccupiedBounds;
        var withWidget = taskbar.Geometry with
        {
            OccupiedBounds = new Int32Rect(tray.X - 120, tray.Y, tray.Width + 120, tray.Height)
        };
        locator.Taskbar = withWidget;
        locator.RaiseChanged();
        Sta.Pump(Settle);

        AssertFillsTheSlotBefore(withWidget, Assert.Single(NativeWindows.WidgetWindows()));
    });

    [Fact]
    public void Follows_the_notification_area_when_it_grows() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);

        var grown = taskbar.GeometryWithTrayOf(520);
        locator.Taskbar = grown;
        locator.RaiseChanged();
        Sta.Pump(Settle);

        AssertFillsTheSlotBefore(grown, Assert.Single(NativeWindows.WidgetWindows()));
        Assert.Equal(1, locator.ActiveWatches);
    });

    [Fact]
    public void Notices_a_change_nobody_reports_on_its_next_periodic_look() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);

        var grown = taskbar.GeometryWithTrayOf(520);
        locator.Taskbar = grown;
        Sta.Pump(PeriodicLook);

        AssertFillsTheSlotBefore(grown, Assert.Single(NativeWindows.WidgetWindows()));
        Assert.Equal(1, locator.ActiveWatches);
    });

    [Fact]
    public void Docks_a_new_window_to_the_taskbar_that_replaces_a_destroyed_one() => Sta.Run(() =>
    {
        var first = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = first.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);
        var firstWindow = Assert.Single(NativeWindows.WidgetWindows());

        // Explorer goes down: its taskbar is destroyed and there is none for a while.
        locator.Taskbar = null;
        first.Dispose();
        Sta.Pump(Settle);

        Assert.Empty(NativeWindows.WidgetWindows());
        Assert.Equal(0, locator.ActiveWatches);

        using var second = new FakeTaskbar();
        locator.Taskbar = second.Geometry;
        Sta.Pump(PeriodicLook);

        var secondWindow = Assert.Single(NativeWindows.WidgetWindows());
        Assert.NotEqual(firstWindow, secondWindow);
        Assert.True(NativeWindows.IsShown(secondWindow));
        Assert.Equal(second.Handle, NativeWindows.ParentOf(secondWindow));
    });

    [Fact]
    public void Docks_a_new_window_when_the_taskbar_changes_without_the_old_one_being_destroyed() => Sta.Run(() =>
    {
        using var first = new FakeTaskbar();
        using var second = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = first.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);

        locator.Taskbar = second.Geometry;
        locator.RaiseChanged();
        Sta.Pump(Settle);

        var window = Assert.Single(NativeWindows.WidgetWindows());
        Assert.Equal(second.Handle, NativeWindows.ParentOf(window));
        Assert.Equal(1, locator.ActiveWatches);
    });

    [Fact]
    public void Stays_hidden_until_shown_again() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);
        var window = Assert.Single(NativeWindows.WidgetWindows());

        host.Hide();
        locator.RaiseChanged();
        Sta.Pump(Settle);

        Assert.False(host.IsVisible);
        Assert.False(NativeWindows.IsShown(window));

        host.Show();

        Assert.True(host.IsVisible);
        Assert.True(NativeWindows.IsShown(window));
    });

    [Fact]
    public void Destroys_its_window_and_stops_watching_on_dispose() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);

        host.Dispose();
        Sta.Pump(Settle);

        Assert.Empty(NativeWindows.WidgetWindows());
        Assert.Equal(0, locator.ActiveWatches);
    });

    private static TaskbarWidgetHost NewHost(FakeTaskbarLocator locator) =>
        new(TestData.NewViewModel(), locator, Dispatcher.CurrentDispatcher, static () => { });

    /// <summary>The widget ends right where the occupied area starts and fills the taskbar's thickness.</summary>
    private static void AssertFillsTheSlotBefore(TaskbarGeometry taskbar, IntPtr window)
    {
        var bounds = NativeWindows.ScreenBounds(window);

        Assert.Equal(taskbar.OccupiedBounds.X, bounds.X + bounds.Width);
        Assert.Equal(taskbar.Bounds.Y, bounds.Y);
        Assert.Equal(taskbar.Bounds.Height, bounds.Height);
    }
}
