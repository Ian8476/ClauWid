namespace ClaudeUsageWidget.Application.Abstractions;

/// <summary>
/// Puerto unico hacia cualquier fuente de consumo. El resultado no trae snapshot cuando la
/// fuente no tiene nada que aportar, para que el orquestador pueda encadenar alternativas, y
/// trae NotBefore cuando la fuente pide que no se le consulte antes de cierta hora.
/// </summary>
public interface IUsageSnapshotProvider
{
    Task<UsageReadResult> GetLatestAsync(CancellationToken cancellationToken);
}
