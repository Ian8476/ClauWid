namespace ClaudeUsageWidget.Application.Abstractions;

/// <summary>Reloj inyectable. Sin esto los formateadores no son testeables.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
