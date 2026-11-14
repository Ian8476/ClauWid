using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Presenters;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class UsageWidgetPresenterTests
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(100);

    // Long enough that no timer fires during a test: every read counted comes from Start or an unlock.
    private static readonly TimeSpan NeverDuringTest = TimeSpan.FromHours(1);

    // Comfortably past the time the presenter keeps the refresh icon turning.
    private static readonly TimeSpan RefreshFeedback = TimeSpan.FromMilliseconds(900);

    [Fact]
    public void Stops_polling_while_the_session_is_locked() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        using var presenter = NewPresenter(provider, ShortInterval);

        presenter.Start();
        Sta.Pump(TimeSpan.FromMilliseconds(500));
        Assert.True(provider.Calls > 1, $"Expected polling while unlocked, got {provider.Calls} reads.");

        SessionEvents.Lock();
        Sta.Pump(TimeSpan.FromMilliseconds(200));
        var readsWhenLocked = provider.Calls;

        Sta.Pump(TimeSpan.FromMilliseconds(600));
        Assert.Equal(readsWhenLocked, provider.Calls);

        SessionEvents.Unlock();
        Sta.Pump(TimeSpan.FromMilliseconds(500));
        Assert.True(provider.Calls > readsWhenLocked + 1, "Expected polling to resume after the unlock.");
    });

    [Fact]
    public void Reads_right_away_on_unlock() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        using var presenter = NewPresenter(provider, NeverDuringTest);

        presenter.Start();
        Assert.Equal(1, provider.Calls);

        SessionEvents.Lock();
        Sta.Pump(TimeSpan.FromMilliseconds(200));
        SessionEvents.Unlock();
        Sta.Pump(TimeSpan.FromMilliseconds(200));

        Assert.Equal(2, provider.Calls);
    });

    [Fact]
    public void Ignores_an_unlock_after_dispose() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var presenter = NewPresenter(provider, NeverDuringTest);

        presenter.Start();
        presenter.Dispose();

        SessionEvents.Unlock();
        Sta.Pump(TimeSpan.FromMilliseconds(200));

        Assert.Equal(1, provider.Calls);
    });

    [Fact]
    public void A_refresh_request_reads_right_away() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, NeverDuringTest, viewModel);

        presenter.Start();
        Assert.Equal(1, provider.Calls);

        viewModel.RequestRefresh();

        Assert.Equal(2, provider.Calls);
        Assert.True(viewModel.IsRefreshing);

        Sta.Pump(RefreshFeedback);
        Assert.False(viewModel.IsRefreshing);
    });

    [Fact]
    public void Requests_made_while_refreshing_do_not_add_reads() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, NeverDuringTest, viewModel);

        presenter.Start();
        viewModel.RequestRefresh();
        viewModel.RequestRefresh();
        viewModel.RequestRefresh();

        Assert.Equal(2, provider.Calls);

        Sta.Pump(RefreshFeedback);
        viewModel.RequestRefresh();

        Assert.Equal(3, provider.Calls);
    });

    [Fact]
    public void A_refresh_request_pushes_the_next_periodic_read_a_whole_interval_away() => Sta.Run(() =>
    {
        var interval = TimeSpan.FromMilliseconds(600);
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, interval, viewModel);

        presenter.Start();
        Sta.Pump(TimeSpan.FromMilliseconds(400));
        viewModel.RequestRefresh();
        Assert.Equal(2, provider.Calls);

        // 800 ms in: the original schedule would have read at 600.
        Sta.Pump(TimeSpan.FromMilliseconds(400));
        Assert.Equal(2, provider.Calls);

        // 1300 ms in: the restarted one reads at 1000.
        Sta.Pump(TimeSpan.FromMilliseconds(500));
        Assert.Equal(3, provider.Calls);
    });

    [Fact]
    public void Ignores_a_refresh_request_after_dispose() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        var presenter = NewPresenter(provider, NeverDuringTest, viewModel);

        presenter.Start();
        presenter.Dispose();
        viewModel.RequestRefresh();

        Assert.Equal(1, provider.Calls);
        Assert.False(viewModel.IsRefreshing);
    });

    private static UsageWidgetPresenter NewPresenter(
        CountingUsageProvider provider, TimeSpan interval, WidgetViewModel? viewModel = null) =>
        new(
            viewModel ?? TestData.NewViewModel(),
            provider,
            new FakeClock(),
            interval,
            interval,
            Dispatcher.CurrentDispatcher);
}
