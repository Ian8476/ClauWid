namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>Parametros de conexion al endpoint de uso. Por defecto, los mismos que usa Claude Code.</summary>
public sealed record ClaudeOAuthUsageOptions
{
    public Uri BaseAddress { get; init; } = new("https://api.anthropic.com/");

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to stop asking after a 429 that carries no Retry-After header. When the
    /// header is present, it wins.
    /// </summary>
    public TimeSpan RateLimitBackoff { get; init; } = TimeSpan.FromMinutes(5);
}
