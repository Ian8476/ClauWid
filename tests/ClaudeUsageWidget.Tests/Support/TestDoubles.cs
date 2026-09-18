using System.Globalization;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Tests.Support;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>Counts reads. Returns no data, which the widget handles as "keep the last reading".</summary>
internal sealed class CountingUsageProvider : IUsageSnapshotProvider
{
    public int Calls { get; private set; }

    public Task<UsageSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<UsageSnapshot?>(null);
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
