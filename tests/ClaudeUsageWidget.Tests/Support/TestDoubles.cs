using System.Globalization;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Tests.Support;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// Counts reads and when each one started, by the real clock. By default it returns no data,
/// which the widget handles as "keep the last reading"; <see cref="Respond"/> says otherwise,
/// and a <see cref="Gate"/> keeps every read in flight until it completes.
/// </summary>
internal sealed class CountingUsageProvider : IUsageSnapshotProvider
{
    public int Calls { get; private set; }

    public List<DateTimeOffset> CallTimes { get; } = [];

    /// <summary>Called once per read, with the number of the read (1 for the first).</summary>
    public Func<int, UsageReadResult> Respond { get; set; } = static _ => UsageReadResult.NoData;

    public Task? Gate { get; set; }

    public async Task<UsageReadResult> GetLatestAsync(CancellationToken cancellationToken)
    {
        Calls++;
        CallTimes.Add(DateTimeOffset.UtcNow);
        var number = Calls;

        if (Gate is { } gate)
        {
            await gate;
        }

        return Respond(number);
    }
}

/// <summary>A widget with no window: it only remembers what it was asked to do.</summary>
internal sealed class FakeWidgetHost : IWidgetHost
{
    public bool IsVisible { get; private set; } = true;

    public int ExitRequests { get; private set; }

    public void Show() => IsVisible = true;

    public void Hide() => IsVisible = false;

    public void Exit() => ExitRequests++;
}

/// <summary>
/// A taskbar that is wherever the test says. <see cref="RaiseChanged"/> plays the part of
/// Windows reporting that it moved.
/// </summary>
internal sealed class FakeTaskbarLocator : ITaskbarLocator
{
    private Action? _onChanged;

    public TaskbarGeometry? Taskbar { get; set; }

    public int ActiveWatches { get; private set; }

    public TaskbarGeometry? Locate() => Taskbar;

    public IDisposable Watch(TaskbarGeometry taskbar, Action onChanged)
    {
        _onChanged = onChanged;
        ActiveWatches++;
        return new Subscription(this);
    }

    public void RaiseChanged() => _onChanged?.Invoke();

    private sealed class Subscription(FakeTaskbarLocator owner) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            owner._onChanged = null;
            owner.ActiveWatches--;
        }
    }
}

internal static class TestData
{
    /// <summary>Wired the same way as the composition root.</summary>
    public static WidgetViewModel NewViewModel()
    {
        var severityPolicy = new UsageSeverityPolicy();
        var percentFormatter = new UsagePercentLabelFormatter();

        return new WidgetViewModel(
            new UsageBarViewModel(new CountdownResetLabelFormatter(), percentFormatter, severityPolicy),
            new UsageBarViewModel(
                new WeekdayResetLabelFormatter(CultureInfo.InvariantCulture, TimeZoneInfo.Utc),
                percentFormatter,
                severityPolicy));
    }

    public static UsageSnapshot Snapshot(DateTimeOffset now, double fiveHourPercent, double sevenDayPercent) => new()
    {
        CapturedAt = now,
        FiveHour = new UsageWindow
        {
            Kind = UsageWindowKind.FiveHour,
            Used = UsagePercentage.FromRaw(fiveHourPercent),
            ResetsAt = now.AddMinutes(133)
        },
        SevenDay = new UsageWindow
        {
            Kind = UsageWindowKind.SevenDay,
            Used = UsagePercentage.FromRaw(sevenDayPercent),
            ResetsAt = now.AddDays(3)
        }
    };
}
