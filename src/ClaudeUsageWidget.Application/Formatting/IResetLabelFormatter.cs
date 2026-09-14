using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Application.Formatting;

/// <summary>Convierte una ventana en la etiqueta izquierda de la barra.</summary>
public interface IResetLabelFormatter
{
    string Format(UsageWindow? window, DateTimeOffset now);
}
