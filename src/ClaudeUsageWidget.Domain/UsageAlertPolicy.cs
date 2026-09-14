namespace ClaudeUsageWidget.Domain;

/// <summary>
/// Regla de negocio: a partir de que punto una ventana deja de ser informativa
/// y pasa a ser una advertencia. Vive en el dominio, no en la UI ni en el simulador.
/// </summary>
public sealed class UsageAlertPolicy
{
    public const double DefaultThresholdPercent = 90d;

    private readonly double _thresholdPercent;

    public UsageAlertPolicy(double thresholdPercent = DefaultThresholdPercent) =>
        _thresholdPercent = thresholdPercent;

    public bool IsAlerting(UsageWindow? window) =>
        window is not null && window.Used.IsAtLeast(_thresholdPercent);
}
