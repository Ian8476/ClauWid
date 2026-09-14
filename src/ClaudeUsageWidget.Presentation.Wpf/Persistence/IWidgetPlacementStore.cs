namespace ClaudeUsageWidget.Presentation.Wpf.Persistence;

public interface IWidgetPlacementStore
{
    WidgetPlacement? Load();

    void Save(WidgetPlacement placement);
}
