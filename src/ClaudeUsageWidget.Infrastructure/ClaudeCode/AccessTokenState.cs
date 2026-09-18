namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>
/// Estado del token de Claude Code en disco. Distinguir "caducado" de "no hay" importa:
/// el caducado lo puede recuperar el propio CLI renovando su sesion; el que falta no.
/// </summary>
public enum AccessTokenState
{
    Missing,
    Expired,
    Usable
}
