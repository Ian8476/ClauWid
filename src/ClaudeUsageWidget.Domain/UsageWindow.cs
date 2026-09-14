namespace ClaudeUsageWidget.Domain;

/// <summary>
/// Estado de una ventana de limite. <see cref="ResetsAt"/> es opcional a proposito:
/// hay fuentes que entregan el porcentaje sin la hora de reinicio.
/// </summary>
public sealed record UsageWindow
{
    public required UsageWindowKind Kind { get; init; }

    public required UsagePercentage Used { get; init; }

    public DateTimeOffset? ResetsAt { get; init; }

    public ResetOrigin ResetOrigin { get; init; } = ResetOrigin.Reported;

    public bool HasKnownReset => ResetsAt.HasValue;

    public bool HasElapsed(DateTimeOffset now) => ResetsAt is { } resetsAt && resetsAt <= now;

    public TimeSpan? RemainingAt(DateTimeOffset now) =>
        ResetsAt is { } resetsAt
            ? (resetsAt > now ? resetsAt - now : TimeSpan.Zero)
            : null;
}
