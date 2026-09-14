namespace ClaudeUsageWidget.Infrastructure.Fake;

/// <summary>Parametros del simulador. Arranca en los valores del mockup.</summary>
public sealed record SimulatedUsageOptions
{
    public double InitialFiveHourPercent { get; init; } = 38d;

    public double InitialSevenDayPercent { get; init; } = 82d;

    public TimeSpan InitialFiveHourRemaining { get; init; } = TimeSpan.FromMinutes(200);

    public double FiveHourPercentPerTick { get; init; } = 2.5d;

    public double SevenDayPercentPerTick { get; init; } = 0.4d;
}
