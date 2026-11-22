using System.Windows.Threading;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Infrastructure.Time;
using ClaudeUsageWidget.Presentation.Wpf.Presenters;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

/// <summary>
/// The presenter runs on the real dispatcher and the real clock, with durations of a few
/// hundred milliseconds. Assertions leave a wide margin around every moment they wait for:
/// they check which side of it things are on, never the exact millisecond.
/// </summary>
public sealed class UsageWidgetPresenterTests
{
    private static readonly TimeSpan NeverDuringTest = TimeSpan.FromHours(1);
    private static readonly TimeSpan Feedback = TimeSpan.FromMilliseconds(150);

    // How much earlier than planned a read may be seen to start: timers and the clock are
    // different sources of time.
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(40);

    [Fact]
    public void Stops_polling_while_the_session_is_locked() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        using var presenter = NewPresenter(provider, Options(poll: Ms(100), spacing: Ms(50)));

        presenter.Start();
        Sta.Pump(Ms(500));
        Assert.True(provider.Calls > 1, $"Expected polling while unlocked, got {provider.Calls} reads.");

        SessionEvents.Lock();
        Sta.Pump(Ms(200));
        var readsWhenLocked = provider.Calls;

        Sta.Pump(Ms(600));
        Assert.Equal(readsWhenLocked, provider.Calls);

        SessionEvents.Unlock();
        Sta.Pump(Ms(500));
        Assert.True(provider.Calls > readsWhenLocked + 1, "Expected polling to resume after the unlock.");
    });

    [Fact]
    public void Reads_right_away_on_unlock() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(100)));

        presenter.Start();
        Assert.Equal(1, provider.Calls);

        SessionEvents.Lock();
        Sta.Pump(Ms(300));
        SessionEvents.Unlock();
        Sta.Pump(Ms(200));

        Assert.Equal(2, provider.Calls);
    });

    [Fact]
    public void Does_not_read_on_unlock_before_the_minimum_spacing_is_over() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(800)));

        presenter.Start();
        SessionEvents.Lock();
        Sta.Pump(Ms(50));
        SessionEvents.Unlock();

        Sta.Pump(Ms(400));
        Assert.Equal(1, provider.Calls);

        Sta.Pump(Ms(700));
        Assert.Equal(2, provider.Calls);
        Assert.True(provider.CallTimes[1] - provider.CallTimes[0] >= Ms(800) - Tolerance);
    });

    [Fact]
    public void Ignores_an_unlock_after_dispose() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(50)));

        presenter.Start();
        presenter.Dispose();

        SessionEvents.Unlock();
        Sta.Pump(Ms(200));

        Assert.Equal(1, provider.Calls);
    });

    [Fact]
    public void A_refresh_request_allowed_by_the_spacing_reads_right_away() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(100)), viewModel);

        presenter.Start();
        Sta.Pump(Ms(200));

        viewModel.RequestRefresh();

        Assert.Equal(2, provider.Calls);
        Assert.Equal(RefreshState.Refreshing, viewModel.RefreshState);

        Sta.Pump(Ms(400));
        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
        Assert.Equal(2, provider.Calls);
    });

    [Fact]
    public void Requests_made_while_refreshing_do_not_add_reads() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(100)), viewModel);

        presenter.Start();
        Sta.Pump(Ms(200));
        viewModel.RequestRefresh();
        viewModel.RequestRefresh();
        viewModel.RequestRefresh();

        Assert.Equal(2, provider.Calls);

        Sta.Pump(Ms(400));
        viewModel.RequestRefresh();

        Assert.Equal(3, provider.Calls);
    });

    [Fact]
    public void A_refresh_request_too_soon_waits_and_reads_once_at_the_earliest_time() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(600)), viewModel);

        presenter.Start();
        viewModel.RequestRefresh();

        Assert.Equal(RefreshState.Waiting, viewModel.RefreshState);
        Assert.Equal(1, provider.Calls);
        Assert.NotNull(viewModel.WaitingUntil);
        Assert.StartsWith("En espera", viewModel.RefreshToolTip, StringComparison.Ordinal);

        Sta.Pump(Ms(300));
        Assert.Equal(RefreshState.Waiting, viewModel.RefreshState);
        Assert.Equal(1, provider.Calls);

        // 900 ms in: the read ran at about 600 and its feedback is over.
        Sta.Pump(Ms(600));
        Assert.Equal(2, provider.Calls);
        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
        Assert.True(provider.CallTimes[1] - provider.CallTimes[0] >= Ms(600) - Tolerance);

        Sta.Pump(Ms(600));
        Assert.Equal(2, provider.Calls);
    });

    [Fact]
    public void Several_requests_while_waiting_give_one_read() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(500)), viewModel);

        presenter.Start();
        viewModel.RequestRefresh();
        Sta.Pump(Ms(100));
        viewModel.RequestRefresh();
        Sta.Pump(Ms(100));
        viewModel.RequestRefresh();

        Assert.Equal(RefreshState.Waiting, viewModel.RefreshState);

        Sta.Pump(Ms(1200));

        Assert.Equal(2, provider.Calls);
        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
    });

    [Fact]
    public void A_request_during_a_read_adds_none_and_follows_it_until_it_completes() => Sta.Run(() =>
    {
        var read = new TaskCompletionSource();
        var provider = new CountingUsageProvider { Gate = read.Task };
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(100)), viewModel);

        presenter.Start();
        Assert.Equal(1, provider.Calls);

        viewModel.RequestRefresh();
        Sta.Pump(Ms(300));

        Assert.Equal(1, provider.Calls);
        Assert.Equal(RefreshState.Refreshing, viewModel.RefreshState);

        read.SetResult();
        Sta.Pump(Ms(300));

        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
        Assert.Equal(1, provider.Calls);
    });

    [Fact]
    public void Retries_at_the_time_the_source_asked_for() => Sta.Run(() =>
    {
        DateTimeOffset notBefore = default;
        var provider = new CountingUsageProvider
        {
            Respond = number =>
            {
                if (number != 1)
                {
                    return UsageReadResult.NoData;
                }

                notBefore = DateTimeOffset.UtcNow + Ms(700);
                return UsageReadResult.WaitUntil(notBefore);
            }
        };
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(100)));

        presenter.Start();
        Sta.Pump(Ms(400));
        Assert.Equal(1, provider.Calls);

        Sta.Pump(Ms(700));
        Assert.Equal(2, provider.Calls);
        Assert.True(provider.CallTimes[1] >= notBefore - Tolerance);
    });

    [Fact]
    public void Waits_the_minimum_spacing_even_when_the_source_asks_for_less() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider
        {
            Respond = number => number == 1 ? UsageReadResult.WaitUntil(DateTimeOffset.UtcNow) : UsageReadResult.NoData
        };
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(600)));

        presenter.Start();
        Sta.Pump(Ms(300));
        Assert.Equal(1, provider.Calls);

        Sta.Pump(Ms(600));
        Assert.Equal(2, provider.Calls);
        Assert.True(provider.CallTimes[1] - provider.CallTimes[0] >= Ms(600) - Tolerance);
    });

    [Fact]
    public void A_refresh_the_source_rejected_goes_back_to_waiting_for_the_retry() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider
        {
            Respond = number => number == 2
                ? UsageReadResult.WaitUntil(DateTimeOffset.UtcNow + Ms(900))
                : UsageReadResult.NoData
        };
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(100)), viewModel);

        presenter.Start();
        Sta.Pump(Ms(200));
        viewModel.RequestRefresh();
        Sta.Pump(Ms(400));

        Assert.Equal(2, provider.Calls);
        Assert.Equal(RefreshState.Waiting, viewModel.RefreshState);

        Sta.Pump(Ms(1200));

        Assert.Equal(3, provider.Calls);
        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
    });

    [Fact]
    public void After_a_success_the_next_read_is_a_poll_interval_away() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider
        {
            Respond = _ => UsageReadResult.Of(TestData.Snapshot(DateTimeOffset.UtcNow, 40, 60))
        };
        using var presenter = NewPresenter(provider, Options(poll: Ms(700), spacing: Ms(100)));

        presenter.Start();
        Sta.Pump(Ms(400));
        Assert.Equal(1, provider.Calls);

        Sta.Pump(Ms(600));
        Assert.Equal(2, provider.Calls);
        Assert.True(provider.CallTimes[1] - provider.CallTimes[0] >= Ms(700) - Tolerance);
    });

    [Fact]
    public void A_refresh_request_pushes_the_next_periodic_read_a_whole_interval_away() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        using var presenter = NewPresenter(provider, Options(poll: Ms(600), spacing: Ms(100)), viewModel);

        presenter.Start();
        Sta.Pump(Ms(400));
        viewModel.RequestRefresh();
        Assert.Equal(2, provider.Calls);

        // 800 ms in: the original schedule would have read at 600.
        Sta.Pump(Ms(400));
        Assert.Equal(2, provider.Calls);

        // 1300 ms in: the one that started over reads at 1000.
        Sta.Pump(Ms(500));
        Assert.Equal(3, provider.Calls);
    });

    [Fact]
    public void Ignores_a_refresh_request_after_dispose() => Sta.Run(() =>
    {
        var provider = new CountingUsageProvider();
        var viewModel = TestData.NewViewModel();
        var presenter = NewPresenter(provider, Options(poll: NeverDuringTest, spacing: Ms(50)), viewModel);

        presenter.Start();
        presenter.Dispose();
        viewModel.RequestRefresh();

        Assert.Equal(1, provider.Calls);
        Assert.Equal(RefreshState.Idle, viewModel.RefreshState);
    });

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static UsagePollingOptions Options(TimeSpan poll, TimeSpan spacing) => new()
    {
        PollInterval = poll,
        MinimumReadSpacing = spacing,
        CountdownInterval = NeverDuringTest,
        MinimumRefreshFeedback = Feedback
    };

    private static UsageWidgetPresenter NewPresenter(
        CountingUsageProvider provider, UsagePollingOptions options, WidgetViewModel? viewModel = null) =>
        new(viewModel ?? TestData.NewViewModel(), provider, new SystemClock(), options, Dispatcher.CurrentDispatcher);
}
