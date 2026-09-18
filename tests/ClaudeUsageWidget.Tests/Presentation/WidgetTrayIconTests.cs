using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Tray;
using ClaudeUsageWidget.Tests.Support;
using Forms = System.Windows.Forms;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class WidgetTrayIconTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Turns_a_whole_refresh_into_a_single_tooltip_update() => Sta.Run(() =>
    {
        var viewModel = TestData.NewViewModel();
        var tray = new WidgetTrayIcon(new Window(), viewModel);
        try
        {
            var notifyIcon = NotifyIconOf(tray);
            var tooltipBefore = notifyIcon.Text;

            var queued = CountQueuedOperations(() => viewModel.Apply(TestData.Snapshot(Now, 42.4, 81.6), Now));

            Assert.Equal(1, queued);
            Assert.Equal(tooltipBefore, notifyIcon.Text);

            Sta.Pump(TimeSpan.FromMilliseconds(50));
            Assert.Contains("42%", notifyIcon.Text, StringComparison.Ordinal);
            Assert.Contains("82%", notifyIcon.Text, StringComparison.Ordinal);
        }
        finally
        {
            tray.Dispose();
        }
    });

    [Fact]
    public void Ignores_changes_the_tooltip_does_not_show() => Sta.Run(() =>
    {
        var viewModel = TestData.NewViewModel();
        var tray = new WidgetTrayIcon(new Window(), viewModel);
        try
        {
            viewModel.Apply(TestData.Snapshot(Now, 42.4, 81.6), Now);
            Sta.Pump(TimeSpan.FromMilliseconds(50));

            // Same rounded labels, different fractions: only the bars move.
            var queued = CountQueuedOperations(() => viewModel.Apply(TestData.Snapshot(Now, 42.2, 81.7), Now));

            Assert.Equal(0, queued);
        }
        finally
        {
            tray.Dispose();
        }
    });

    [Fact]
    public void Disposing_with_an_update_queued_does_not_throw() => Sta.Run(() =>
    {
        var viewModel = TestData.NewViewModel();
        var tray = new WidgetTrayIcon(new Window(), viewModel);

        viewModel.Apply(TestData.Snapshot(Now, 55, 90), Now);
        tray.Dispose();

        Sta.Pump(TimeSpan.FromMilliseconds(50));
    });

    private static int CountQueuedOperations(Action action)
    {
        var hooks = Dispatcher.CurrentDispatcher.Hooks;
        var queued = 0;
        DispatcherHookEventHandler count = (_, _) => queued++;

        hooks.OperationPosted += count;
        try
        {
            action();
        }
        finally
        {
            hooks.OperationPosted -= count;
        }

        return queued;
    }

    private static Forms.NotifyIcon NotifyIconOf(WidgetTrayIcon tray) =>
        typeof(WidgetTrayIcon)
            .GetField("_notifyIcon", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(tray) as Forms.NotifyIcon
        ?? throw new InvalidOperationException("WidgetTrayIcon no longer keeps its NotifyIcon in _notifyIcon.");
}
