using System.ComponentModel;
using System.Diagnostics;
using ClaudeUsageWidget.Application.Abstractions;

namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>
/// Le pide al CLI de Claude Code que revise su sesion cuando el token del archivo caduco.
/// El widget sigue sin renovarlo por su cuenta: rotar el refresh token a espaldas de Claude
/// Code dejaria al CLI con uno invalidado. Aqui solo se lanza el CLI sin ventana y es el
/// propio Claude Code quien decide si toca renovar y reescribe .credentials.json, igual que
/// cuando el usuario lo abre a mano.
/// </summary>
public sealed class ClaudeCliSessionRefresher
{
    private const string PathVariable = "PATH";
    private const string CommandProcessorName = "cmd.exe";
    private const string BatchExtension = ".cmd";
    private const string LegacyBatchExtension = ".bat";

    // El orden importa: gana el ejecutable nativo sobre el .cmd que instala npm.
    private static readonly string[] ExecutableExtensions = [".exe", BatchExtension, LegacyBatchExtension];

    private readonly IClock _clock;
    private readonly ClaudeCliSessionRefresherOptions _options;

    private DateTimeOffset? _lastAttemptAt;

    public ClaudeCliSessionRefresher(IClock clock, ClaudeCliSessionRefresherOptions options)
    {
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// True si el CLI llego a ejecutarse, sin afirmar que haya renovado nada: eso lo dice la
    /// siguiente lectura del archivo de credenciales.
    /// </summary>
    public async Task<bool> TryRenewSessionAsync(CancellationToken cancellationToken)
    {
        if (_lastAttemptAt is { } lastAttemptAt && _clock.Now - lastAttemptAt < _options.MinimumInterval)
        {
            return false;
        }

        if (FindExecutable() is not { } executablePath)
        {
            Debug.WriteLine(
                $"Claude usage: no se encontro '{_options.ExecutableName}' en el PATH. " +
                "La sesion la tiene que renovar Claude Code.");

            return false;
        }

        _lastAttemptAt = _clock.Now;

        try
        {
            using var process = Process.Start(BuildStartInfo(executablePath));
            if (process is null)
            {
                return false;
            }

            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attempt.CancelAfter(_options.Timeout);

            try
            {
                await process.WaitForExitAsync(attempt.Token);
            }
            catch (OperationCanceledException)
            {
                // El .cmd de npm arranca node como hijo, asi que se mata el arbol entero.
                TryKill(process);
                cancellationToken.ThrowIfCancellationRequested();

                Debug.WriteLine(
                    $"Claude usage: {_options.ExecutableName} no respondio en {_options.Timeout.TotalSeconds:0} s.");

                return false;
            }

            Debug.WriteLine($"Claude usage: {_options.ExecutableName} termino con codigo {process.ExitCode}.");
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Claude usage: no se pudo lanzar {_options.ExecutableName}. {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sin ventana y desde el perfil del usuario, no desde el directorio del widget. Un .cmd,
    /// que es como lo instala npm, no es un ejecutable: lo tiene que arrancar cmd.exe, y se
    /// toma el de System32 en vez del que diga la variable ComSpec.
    /// </summary>
    private ProcessStartInfo BuildStartInfo(string executablePath)
    {
        var isBatchFile = Path.GetExtension(executablePath).ToLowerInvariant()
            is BatchExtension or LegacyBatchExtension;

        var startInfo = new ProcessStartInfo
        {
            FileName = isBatchFile
                ? Path.Combine(Environment.SystemDirectory, CommandProcessorName)
                : executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (isBatchFile)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(executablePath);
        }

        foreach (var argument in _options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// El PATH se recorre a mano porque CreateProcess solo completa ".exe": pedir "claude"
    /// a secas no encontraria el claude.cmd de una instalacion con npm.
    /// </summary>
    private string? FindExecutable()
    {
        var directories = (Environment.GetEnvironmentVariable(PathVariable) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in ExecutableExtensions)
            {
                var candidate = Path.Combine(directory.Trim('"'), _options.ExecutableName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // Termino por su cuenta entre el timeout y el intento de matarlo.
        }
    }
}
