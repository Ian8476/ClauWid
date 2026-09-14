using System.Globalization;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Application.Formatting;

/// <summary>
/// Momento absoluto para la ventana de 7 dias: "Sun 5:00 PM".
/// Una cuenta regresiva de varios dias no le dice nada util a nadie.
/// </summary>
public sealed class WeekdayResetLabelFormatter : IResetLabelFormatter
{
    private const string Pattern = "ddd h:mm tt";

    private readonly CultureInfo _culture;
    private readonly TimeZoneInfo _timeZone;

    public WeekdayResetLabelFormatter(CultureInfo culture, TimeZoneInfo timeZone)
    {
        _culture = culture;
        _timeZone = timeZone;
    }

    public string Format(UsageWindow? window, DateTimeOffset now)
    {
        if (window?.ResetsAt is not { } resetsAt)
        {
            return ResetLabelPlaceholders.Unknown;
        }

        var prefix = window.ResetOrigin is ResetOrigin.Estimated
            ? ResetLabelPlaceholders.EstimatedPrefix
            : string.Empty;

        var local = TimeZoneInfo.ConvertTime(resetsAt, _timeZone);

        return prefix + local.ToString(Pattern, _culture);
    }
}
