using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Application.Abstractions;

/// <summary>
/// Puerto unico hacia cualquier fuente de consumo. Devuelve null cuando la fuente
/// no tiene nada que aportar, para que el orquestador pueda encadenar alternativas.
/// </summary>
public interface IUsageSnapshotProvider
{
    Task<UsageSnapshot?> GetLatestAsync(CancellationToken cancellationToken);
}
