namespace ClaudeUsageWidget.Presentation.Wpf.Persistence;

/// <summary>Una posicion por caso de monitores; la clave identifica el caso.</summary>
public interface IWidgetPlacementStore
{
    WidgetPlacement? Load(string layoutKey);

    void Save(string layoutKey, WidgetPlacement placement);
}
