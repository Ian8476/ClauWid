using ClaudeUsageWidget.Application.Abstractions;

namespace ClaudeUsageWidget.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
