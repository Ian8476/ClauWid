namespace ClaudeUsageWidget.Domain;

/// <summary>How close a limit window is to running out, from plenty of room to nearly exhausted.</summary>
public enum UsageSeverity
{
    Low,
    Moderate,
    High,
    Critical
}
