using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeUsageWidget.Application.Abstractions;

namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>
/// Lee el token OAuth que Claude Code deja en .credentials.json al iniciar sesion.
/// Solo lectura a proposito: renovar el token desde aqui rotaria el refresh token y
/// dejaria a Claude Code con uno invalidado. Un token caducado cuenta como "sin dato"
/// hasta que Claude Code lo renueve en su proximo uso.
/// </summary>
public sealed class ClaudeCodeCredentialsReader
{
    private const string ConfigDirectoryVariable = "CLAUDE_CONFIG_DIR";
    private const string DefaultConfigFolderName = ".claude";
    private const string CredentialsFileName = ".credentials.json";

    private readonly IClock _clock;
    private readonly string _credentialsPath;

    public ClaudeCodeCredentialsReader(IClock clock)
    {
        _clock = clock;
        _credentialsPath = Path.Combine(ResolveConfigDirectory(), CredentialsFileName);
    }

    /// <summary>Null si no hay sesion iniciada, el archivo no se puede leer o el token caduco.</summary>
    public string? ReadAccessToken()
    {
        var credentials = TryReadCredentials();

        if (credentials?.AccessToken is not { Length: > 0 } accessToken)
        {
            Debug.WriteLine($"Claude usage: no hay token OAuth de Claude Code en {_credentialsPath}.");
            return null;
        }

        if (credentials.ExpiresAt is { } expiresAtMilliseconds
            && DateTimeOffset.FromUnixTimeMilliseconds(expiresAtMilliseconds) <= _clock.Now)
        {
            Debug.WriteLine("Claude usage: el token OAuth caduco. Claude Code lo renueva en su proximo uso.");
            return null;
        }

        return accessToken;
    }

    private OAuthCredentials? TryReadCredentials()
    {
        try
        {
            // Claude Code puede estar reescribiendo el archivo justo ahora: no se le bloquea.
            using var stream = new FileStream(
                _credentialsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            return JsonSerializer.Deserialize<CredentialsFile>(stream)?.ClaudeAiOauth;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string ResolveConfigDirectory() =>
        Environment.GetEnvironmentVariable(ConfigDirectoryVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DefaultConfigFolderName);

    private sealed record CredentialsFile(
        [property: JsonPropertyName("claudeAiOauth")] OAuthCredentials? ClaudeAiOauth);

    private sealed record OAuthCredentials(
        [property: JsonPropertyName("accessToken")] string? AccessToken,
        [property: JsonPropertyName("expiresAt")] long? ExpiresAt);
}
