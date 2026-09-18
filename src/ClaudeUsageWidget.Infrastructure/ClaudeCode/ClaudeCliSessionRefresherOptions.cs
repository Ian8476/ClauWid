namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>Como se invoca al CLI de Claude Code para que revise y renueve su sesion.</summary>
public sealed record ClaudeCliSessionRefresherOptions
{
    /// <summary>Se busca en el PATH; la extension la pone el buscador.</summary>
    public string ExecutableName { get; init; } = "claude";

    /// <summary>
    /// "doctor" revisa la instalacion y la sesion sin mandar ningun mensaje al modelo, asi que
    /// no consume cuota, y al comprobar la sesion renueva el token caducado y reescribe
    /// .credentials.json. "auth status" no sirve: solo lee el archivo, sin renovar nada.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = ["doctor"];

    /// <summary>Arrancar el CLI tarda un poco; pasado este tiempo se da por fallido.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sin sesion iniciada, cada consulta volveria a lanzar el CLI. Un intento cada cuarto de
    /// hora basta para recuperarse solo sin castigar la maquina.
    /// </summary>
    public TimeSpan MinimumInterval { get; init; } = TimeSpan.FromMinutes(15);
}
