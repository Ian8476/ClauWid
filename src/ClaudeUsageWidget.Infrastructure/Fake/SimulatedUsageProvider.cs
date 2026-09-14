using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Infrastructure.Fake;

/// <summary>
/// Fuente falsa para desarrollar la UI sin depender de Claude. Sube el consumo en
/// cada lectura y reinicia la ventana de 5 horas al llegar al tope, de modo que los
/// estados de umbral, animacion y reinicio se puedan ver sin esperar horas reales.
/// </summary>
public sealed class SimulatedUsageProvider : IUsageSnapshotProvider
{
    private readonly IClock _clock;
    private readonly SimulatedUsageOptions _options;

    private double _fiveHourPercent;
    private double _sevenDayPercent;
    private DateTimeOffset _fiveHourResetsAt;
    private DateTimeOffset _sevenDayResetsAt;
    private bool _isInitialized;

    public SimulatedUsageProvider(IClock clock, SimulatedUsageOptions options)
    {
        _clock = clock;
        _options = options;
    }

    public Task<UsageSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        var now = _clock.Now;

        EnsureInitialized(now);
        Advance(now);

        var snapshot = new UsageSnapshot
        {
            CapturedAt = now,
            FiveHour = new UsageWindow
            {
                Kind = UsageWindowKind.FiveHour,
                Used = UsagePercentage.FromRaw(_fiveHourPercent),
                ResetsAt = _fiveHourResetsAt,
                ResetOrigin = ResetOrigin.Reported
            },
            SevenDay = new UsageWindow
            {
                Kind = UsageWindowKind.SevenDay,
                Used = UsagePercentage.FromRaw(_sevenDayPercent),
                ResetsAt = _sevenDayResetsAt,
                ResetOrigin = ResetOrigin.Estimated
            }
        };

        return Task.FromResult<UsageSnapshot?>(snapshot);
    }

    private void EnsureInitialized(DateTimeOffset now)
    {
        if (_isInitialized)
        {
            return;
        }

        _fiveHourPercent = _options.InitialFiveHourPercent;
        _sevenDayPercent = _options.InitialSevenDayPercent;
        _fiveHourResetsAt = now + _options.InitialFiveHourRemaining;
        _sevenDayResetsAt = NextSundayAtFivePm(now);
        _isInitialized = true;
    }

    private void Advance(DateTimeOffset now)
    {
        _fiveHourPercent += _options.FiveHourPercentPerTick;
        _sevenDayPercent += _options.SevenDayPercentPerTick;

        if (_fiveHourPercent > UsagePercentage.Maximum || _fiveHourResetsAt <= now)
        {
            _fiveHourPercent = 0d;
            _fiveHourResetsAt = now + TimeSpan.FromHours(5);
        }

        if (_sevenDayPercent > UsagePercentage.Maximum)
        {
            _sevenDayPercent = 0d;
            _sevenDayResetsAt = NextSundayAtFivePm(now);
        }
    }

    private static DateTimeOffset NextSundayAtFivePm(DateTimeOffset now)
    {
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, 17, 0, 0, now.Offset)
            .AddDays(daysUntilSunday);

        return candidate <= now ? candidate.AddDays(7) : candidate;
    }
}
