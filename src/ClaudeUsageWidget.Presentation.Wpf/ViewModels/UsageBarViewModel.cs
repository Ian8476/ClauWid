using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Presentation.Wpf.ViewModels;

/// <summary>
/// Estado de una barra listo para pintar. Deliberadamente sin tipos de WPF:
/// los pinceles se eligen en XAML a partir de <see cref="IsAlerting"/>.
/// </summary>
public sealed class UsageBarViewModel : ObservableObject
{
    private readonly IResetLabelFormatter _resetLabelFormatter;
    private readonly UsagePercentLabelFormatter _percentLabelFormatter;
    private readonly UsageAlertPolicy _alertPolicy;

    private double _fraction;
    private string _resetLabel = ResetLabelPlaceholders.Unknown;
    private string _percentLabel = ResetLabelPlaceholders.Unknown;
    private bool _isAlerting;
    private bool _hasData;

    public UsageBarViewModel(
        IResetLabelFormatter resetLabelFormatter,
        UsagePercentLabelFormatter percentLabelFormatter,
        UsageAlertPolicy alertPolicy)
    {
        _resetLabelFormatter = resetLabelFormatter;
        _percentLabelFormatter = percentLabelFormatter;
        _alertPolicy = alertPolicy;
    }

    public double Fraction
    {
        get => _fraction;
        private set => SetField(ref _fraction, value);
    }

    public string ResetLabel
    {
        get => _resetLabel;
        private set => SetField(ref _resetLabel, value);
    }

    public string PercentLabel
    {
        get => _percentLabel;
        private set => SetField(ref _percentLabel, value);
    }

    public bool IsAlerting
    {
        get => _isAlerting;
        private set => SetField(ref _isAlerting, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetField(ref _hasData, value);
    }

    public void Apply(UsageWindow? window, DateTimeOffset now)
    {
        HasData = window is not null;
        Fraction = window?.Used.AsFraction ?? 0d;
        PercentLabel = _percentLabelFormatter.Format(window);
        ResetLabel = _resetLabelFormatter.Format(window, now);
        IsAlerting = _alertPolicy.IsAlerting(window);
    }
}
