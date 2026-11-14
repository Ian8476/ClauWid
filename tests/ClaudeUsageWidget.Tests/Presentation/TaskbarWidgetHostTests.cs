using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Presentation.Wpf.Windows;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarWidgetHostTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(200);

    // Past the host's once-a-second look for a taskbar that is not there yet.
    private static readonly TimeSpan TaskbarRetry = TimeSpan.FromMilliseconds(1500);

    [Fact]
    public void Docks_before_the_notification_area_owned_by_the_taskbar() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);

        host.Start();
        Sta.Pump(Settle);

        var window = Assert.Single(WidgetWindows());
        Assert.True(window.IsVisible);
        Assert.Equal(taskbar.Handle, new WindowInteropHelper(window).Owner);
        AssertFillsTheSlotBefore(taskbar.Geometry, window);
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

        AssertFillsTheSlotBefore(grown, Assert.Single(WidgetWindows()));
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
        var firstWindow = Assert.Single(WidgetWindows());

        // Explorer goes down: its taskbar is destroyed and there is none for a while.
        locator.Taskbar = null;
        first.Dispose();
        Sta.Pump(Settle);

        Assert.Empty(WidgetWindows());
        Assert.Equal(0, locator.ActiveWatches);

        using var second = new FakeTaskbar();
        locator.Taskbar = second.Geometry;
        Sta.Pump(TaskbarRetry);

        var secondWindow = Assert.Single(WidgetWindows());
        Assert.NotSame(firstWindow, secondWindow);
        Assert.True(secondWindow.IsVisible);
        Assert.Equal(second.Handle, new WindowInteropHelper(secondWindow).Owner);
    });

    [Fact]
    public void Stays_hidden_until_shown_again() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        using var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);
        var window = Assert.Single(WidgetWindows());

        host.Hide();
        locator.RaiseChanged();
        Sta.Pump(Settle);

        Assert.False(host.IsVisible);
        Assert.False(window.IsVisible);

        host.Show();

        Assert.True(host.IsVisible);
        Assert.True(window.IsVisible);
    });

    [Fact]
    public void Closes_its_window_and_stops_watching_on_dispose() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        var locator = new FakeTaskbarLocator { Taskbar = taskbar.Geometry };
        var host = NewHost(locator);
        host.Start();
        Sta.Pump(Settle);

        host.Dispose();
        Sta.Pump(Settle);

        Assert.Empty(WidgetWindows());
        Assert.Equal(0, locator.ActiveWatches);
    });

    private static TaskbarWidgetHost NewHost(FakeTaskbarLocator locator) =>
        new(TestData.NewViewModel(), locator, Dispatcher.CurrentDispatcher, static () => { });

    /// <summary>The widget windows alive on this thread. A closed window drops out of this list.</summary>
    private static List<TaskbarWindow> WidgetWindows() =>
        PresentationSource.CurrentSources
            .OfType<HwndSource>()
            .Select(source => source.RootVisual)
            .OfType<TaskbarWindow>()
            .ToList();

    private static void AssertFillsTheSlotBefore(TaskbarGeometry taskbar, TaskbarWindow window)
    {
        var dpiScale = VisualTreeHelper.GetDpi(window).DpiScaleX;

        Assert.Equal(taskbar.TrayBounds.X, (window.Left + window.Width) * dpiScale, precision: 3);
        Assert.Equal(taskbar.Bounds.Y, window.Top * dpiScale, precision: 3);
        Assert.Equal(taskbar.Bounds.Height, window.Height * dpiScale, precision: 3);
    }
}
