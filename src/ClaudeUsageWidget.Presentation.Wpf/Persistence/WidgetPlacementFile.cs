namespace ClaudeUsageWidget.Presentation.Wpf.Persistence;

/// <summary>
/// Contenido de placement.json: una posicion por caso de monitores, con claves como
/// "1920x1080", "2560x1440 + 1920x1080@2560,0" o "3840x2160".
/// </summary>
internal sealed class WidgetPlacementFile
{
    public Dictionary<string, WidgetPlacement>? Layouts { get; set; } = new();
}
