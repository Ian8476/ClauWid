namespace ClaudeUsageWidget.Domain;

/// <summary>
/// Lectura puntual del consumo. Ambas ventanas son opcionales porque una fuente
/// puede conocer solo una de las dos.
/// </summary>
public sealed record UsageSnapshot
{
    public required DateTimeOffset CapturedAt { get; init; }

    public UsageWindow? FiveHour { get; init; }

    public UsageWindow? SevenDay { get; init; }

    public bool IsEmpty => FiveHour is null && SevenDay is null;

    public TimeSpan AgeAt(DateTimeOffset now) => now > CapturedAt ? now - CapturedAt : TimeSpan.Zero;

    public static UsageSnapshot Empty(DateTimeOffset capturedAt) => new() { CapturedAt = capturedAt };
}
