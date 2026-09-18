namespace ClaudeUsageWidget.Domain;

/// <summary>
/// Business rule: which severity band a window falls into. Both windows share the same
/// bands, so a color means the same thing on either bar. Lives in the domain, not in the
/// UI or the simulator. Each threshold is where its band starts, inclusive.
/// </summary>
public sealed class UsageSeverityPolicy
{
    public const double DefaultModerateFromPercent = 50d;
    public const double DefaultHighFromPercent = 70d;
    public const double DefaultCriticalFromPercent = 85d;

    private readonly double _moderateFromPercent;
    private readonly double _highFromPercent;
    private readonly double _criticalFromPercent;

    public UsageSeverityPolicy(
        double moderateFromPercent = DefaultModerateFromPercent,
        double highFromPercent = DefaultHighFromPercent,
        double criticalFromPercent = DefaultCriticalFromPercent)
    {
        _moderateFromPercent = moderateFromPercent;
        _highFromPercent = highFromPercent;
        _criticalFromPercent = criticalFromPercent;
    }

    public UsageSeverity Classify(UsageWindow? window) => window?.Used switch
    {
        null => UsageSeverity.Low,
        { } used when used.IsAtLeast(_criticalFromPercent) => UsageSeverity.Critical,
        { } used when used.IsAtLeast(_highFromPercent) => UsageSeverity.High,
        { } used when used.IsAtLeast(_moderateFromPercent) => UsageSeverity.Moderate,
        _ => UsageSeverity.Low
    };
}
