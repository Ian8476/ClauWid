namespace ClaudeUsageWidget.Domain;

/// <summary>
/// De donde salio la hora de reinicio. La UI debe distinguirlas:
/// un valor derivado por nosotros no puede presentarse como un dato reportado.
/// </summary>
public enum ResetOrigin
{
    Reported,
    Estimated
}
