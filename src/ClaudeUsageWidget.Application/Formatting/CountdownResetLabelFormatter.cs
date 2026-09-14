using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Application.Formatting;

/// <summary>
/// Cuenta regresiva corta para la ventana de 5 horas: "3:20h".
/// Trunca los segundos hacia abajo para que el numero nunca prometa mas tiempo del que queda.
/// </summary>
public sealed class CountdownResetLabelFormatter : IResetLabelFormatter
{
    private const string HourSuffix = "h";

    public string Format(UsageWindow? window, DateTimeOffset now)
    {
        if (window?.RemainingAt(now) is not { } remaining)
        {
            return ResetLabelPlaceholders.Unknown;
        }

        var prefix = window.ResetOrigin is ResetOrigin.Estimated
            ? ResetLabelPlaceholders.EstimatedPrefix
            : string.Empty;

        var hours = (int)remaining.TotalHours;
        var minutes = remaining.Minutes;

        return $"{prefix}{hours}:{minutes:00}{HourSuffix}";
    }
}
