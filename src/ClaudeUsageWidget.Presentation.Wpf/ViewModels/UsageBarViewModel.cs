using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Presentation.Wpf.ViewModels;

/// <summary>
/// State of one bar, ready to paint. Deliberately free of WPF types:
/// the fill brush is picked in XAML from <see cref="Severity"/>.
/// </summary>
public sealed class UsageBarViewModel : ObservableObject
{
    private readonly IResetLabelFormatter _resetLabelFormatter;
    private readonly UsagePercentLabelFormatter _percentLabelFormatter;
    private readonly UsageSeverityPolicy _severityPolicy;

    private double _fraction;
    private string _resetLabel = ResetLabelPlaceholders.Unknown;
    private string _percentLabel = ResetLabelPlaceholders.Unknown;
    private UsageSeverity _severity;
    private bool _hasData;

    public UsageBarViewModel(
        IResetLabelFormatter resetLabelFormatter,
        UsagePercentLabelFormatter percentLabelFormatter,
        UsageSeverityPolicy severityPolicy)
    {
        _resetLabelFormatter = resetLabelFormatter;
        _percentLabelFormatter = percentLabelFormatter;
        _severityPolicy = severityPolicy;
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

    public UsageSeverity Severity
    {
        get => _severity;
        private set => SetField(ref _severity, value);
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
        Severity = _severityPolicy.Classify(window);
    }
}
