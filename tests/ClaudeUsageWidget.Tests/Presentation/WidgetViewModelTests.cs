using ClaudeUsageWidget.Presentation.Wpf;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class WidgetViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Says_what_a_click_does_while_idle_and_while_refreshing()
    {
        var viewModel = TestData.NewViewModel();

        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
        Assert.Equal(WidgetText.RefreshIdleToolTip, viewModel.RefreshToolTip);

        viewModel.RefreshState = RefreshState.Refreshing;

        Assert.Equal(WidgetText.RefreshingToolTip, viewModel.RefreshToolTip);
    }

    [Fact]
    public void Says_when_a_waiting_refresh_will_run_in_local_time()
    {
        var viewModel = TestData.NewViewModel();
        var runsAt = new DateTimeOffset(2026, 9, 18, 12, 4, 56, TimeSpan.Zero);

        viewModel.WaitingUntil = runsAt;
        viewModel.RefreshState = RefreshState.Waiting;

        var expectedTime = runsAt.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.StartsWith("En espera:", viewModel.RefreshToolTip, StringComparison.Ordinal);
        Assert.EndsWith($"Se actualizará a las {expectedTime}", viewModel.RefreshToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Raises_the_tooltip_changed_with_the_state_and_with_the_waiting_time()
    {
        var viewModel = TestData.NewViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.RefreshState = RefreshState.Waiting;
        viewModel.WaitingUntil = Now;

        Assert.Equal(
            [nameof(WidgetViewModel.RefreshState), nameof(WidgetViewModel.RefreshToolTip),
             nameof(WidgetViewModel.WaitingUntil), nameof(WidgetViewModel.RefreshToolTip)],
            changed);
    }

    [Fact]
    public void Has_no_data_until_a_snapshot_arrives()
    {
        var viewModel = TestData.NewViewModel();

        Assert.Null(viewModel.LastUpdatedAt);
        Assert.Equal(WidgetText.NoDataYet, viewModel.UpdatedLabel);

        viewModel.Apply(null, Now);

        Assert.Null(viewModel.LastUpdatedAt);
        Assert.Equal(WidgetText.NoDataYet, viewModel.UpdatedLabel);
    }

    [Fact]
    public void Says_when_the_data_was_read_in_local_time_and_keeps_it_when_a_read_brings_nothing()
    {
        var viewModel = TestData.NewViewModel();
        var capturedAt = new DateTimeOffset(2026, 9, 18, 11, 42, 30, TimeSpan.Zero);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Apply(TestData.Snapshot(capturedAt, 40, 60), Now);

        var expectedTime = capturedAt.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(capturedAt, viewModel.LastUpdatedAt);
        Assert.Equal($"Actualizado a las {expectedTime}", viewModel.UpdatedLabel);
        Assert.Contains(nameof(WidgetViewModel.UpdatedLabel), changed);

        viewModel.Apply(null, Now.AddMinutes(5));

        Assert.Equal(capturedAt, viewModel.LastUpdatedAt);
    }
}
