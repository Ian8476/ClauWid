using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Application.Formatting;

/// <summary>Etiqueta derecha de la barra: "38%".</summary>
public sealed class UsagePercentLabelFormatter
{
    public string Format(UsageWindow? window) =>
        window is null ? ResetLabelPlaceholders.Unknown : window.Used.ToString();
}
