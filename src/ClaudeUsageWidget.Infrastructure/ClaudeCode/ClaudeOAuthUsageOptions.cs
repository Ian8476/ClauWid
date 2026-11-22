namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>Parametros de conexion al endpoint de uso. Por defecto, los mismos que usa Claude Code.</summary>
public sealed record ClaudeOAuthUsageOptions
{
    public Uri BaseAddress { get; init; } = new("https://api.anthropic.com/");

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to stop asking after a 429 that carries no Retry-After header. When the
    /// header is present, it wins, unless it asks for less than <see cref="RateLimitFloor"/>.
    /// </summary>
    public TimeSpan RateLimitBackoff { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The least a 429 holds off the next read, whatever its Retry-After says. The API answers
    /// "Retry-After: 0" and then keeps rejecting for well over a minute (measured 2026-11-22:
    /// reads 3, 45, 60 and 75 s after a success were rejected, reads 105 and 120 s after one
    /// were served).
    /// </summary>
    public TimeSpan RateLimitFloor { get; init; } = TimeSpan.FromSeconds(105);
}
