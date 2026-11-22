using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Application.Abstractions;

/// <summary>
/// What one read of a usage source came back with. <see cref="Snapshot"/> is null when the
/// source had nothing to contribute. <see cref="NotBefore"/> is set when the source asked not
/// to be read again until then, as a rate-limited API does: whoever schedules reads must not
/// start another one earlier.
/// </summary>
public sealed record UsageReadResult(UsageSnapshot? Snapshot, DateTimeOffset? NotBefore = null)
{
    /// <summary>Nothing to show and no request to wait: keep the last reading and try again as usual.</summary>
    public static UsageReadResult NoData { get; } = new(Snapshot: null);

    public static UsageReadResult Of(UsageSnapshot? snapshot) => snapshot is null ? NoData : new(snapshot);

    /// <summary>The source has nothing now and asks not to be read again before <paramref name="notBefore"/>.</summary>
    public static UsageReadResult WaitUntil(DateTimeOffset notBefore) => new(null, notBefore);
}
