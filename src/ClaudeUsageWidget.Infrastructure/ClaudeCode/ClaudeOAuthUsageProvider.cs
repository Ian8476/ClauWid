using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>
/// Consumo real de la suscripcion: el mismo endpoint que consulta /usage dentro de Claude Code,
/// autenticado con la sesion de Claude Code. No esta documentado publicamente, asi que un
/// cambio de formato debe acabar en "sin dato", nunca en un numero inventado.
/// </summary>
public sealed class ClaudeOAuthUsageProvider : IUsageSnapshotProvider, IDisposable
{
    private const string UsagePath = "api/oauth/usage";
    private const string BearerScheme = "Bearer";
    private const string BetaHeaderName = "anthropic-beta";
    private const string OAuthBetaVersion = "oauth-2025-04-20";
    private const string UserAgentProduct = "ClaudeUsageWidget";
    private const string UserAgentVersion = "1.0";

    private readonly ClaudeCodeCredentialsReader _credentialsReader;
    private readonly IClock _clock;
    private readonly HttpClient _httpClient;

    public ClaudeOAuthUsageProvider(
        ClaudeCodeCredentialsReader credentialsReader,
        IClock clock,
        ClaudeOAuthUsageOptions options)
    {
        _credentialsReader = credentialsReader;
        _clock = clock;
        _httpClient = new HttpClient
        {
            BaseAddress = options.BaseAddress,
            Timeout = options.RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(UserAgentProduct, UserAgentVersion));
    }

    public async Task<UsageSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        // El archivo se relee en cada consulta: Claude Code renueva el token por su cuenta.
        if (_credentialsReader.ReadAccessToken() is not { } accessToken)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UsagePath);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);
        request.Headers.Add(BetaHeaderName, OAuthBetaVersion);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Token revocado, o emitido sin el scope user:profile (p. ej. "claude setup-token").
            Debug.WriteLine($"Claude usage: el endpoint rechazo el token ({(int)response.StatusCode}).");
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var usage = await JsonSerializer.DeserializeAsync<UsageResponse>(body, cancellationToken: cancellationToken);

        var snapshot = new UsageSnapshot
        {
            CapturedAt = _clock.Now,
            FiveHour = ToWindow(UsageWindowKind.FiveHour, usage?.FiveHour),
            SevenDay = ToWindow(UsageWindowKind.SevenDay, usage?.SevenDay)
        };

        return snapshot.IsEmpty ? null : snapshot;
    }

    public void Dispose() => _httpClient.Dispose();

    /// <summary>utilization ya viene en escala 0-100, igual que el "N% used" de Claude Code.</summary>
    private static UsageWindow? ToWindow(UsageWindowKind kind, UsageLimit? limit)
    {
        if (limit?.Utilization is not { } utilization)
        {
            return null;
        }

        return new UsageWindow
        {
            Kind = kind,
            Used = UsagePercentage.FromRaw(utilization),
            ResetsAt = ParseResetsAt(limit.ResetsAt),
            ResetOrigin = ResetOrigin.Reported
        };
    }

    /// <summary>
    /// La API entrega ISO 8601. Se aceptan tambien segundos Unix, que Claude Code maneja en
    /// otras rutas, para que un cambio de forma no rompa la deserializacion completa.
    /// Sin reinicio activo llega null y la etiqueta muestra "--".
    /// </summary>
    private static DateTimeOffset? ParseResetsAt(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.String } text
            when DateTimeOffset.TryParse(
                text.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            => parsed,
        { ValueKind: JsonValueKind.Number } number
            when number.TryGetInt64(out var unixSeconds)
            => DateTimeOffset.FromUnixTimeSeconds(unixSeconds),
        _ => null
    };

    private sealed record UsageResponse(
        [property: JsonPropertyName("five_hour")] UsageLimit? FiveHour,
        [property: JsonPropertyName("seven_day")] UsageLimit? SevenDay);

    private sealed record UsageLimit(
        [property: JsonPropertyName("utilization")] double? Utilization,
        [property: JsonPropertyName("resets_at")] JsonElement? ResetsAt);
}
