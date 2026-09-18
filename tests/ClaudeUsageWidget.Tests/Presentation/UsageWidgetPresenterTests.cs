using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Presenters;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class UsageWidgetPresenterTests
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(100);

    // Long enough that no timer fires during a test: every read counted comes from Start or an unlock.
    private static readonly TimeSpan NeverDuringTest = TimeSpan.FromHours(1);

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

    private static UsageWidgetPresenter NewPresenter(CountingUsageProvider provider, TimeSpan interval) =>
        new(TestData.NewViewModel(), provider, new FakeClock(), interval, interval, Dispatcher.CurrentDispatcher);
}
