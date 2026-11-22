namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// What the widget's menu can ask for. The values double as the menu items' command ids,
/// so none of them may be 0: that is what a menu that was dismissed returns.
/// </summary>
public enum MenuCommand
{
    Refresh = 1,
    Hide = 2,
    Exit = 3
}
