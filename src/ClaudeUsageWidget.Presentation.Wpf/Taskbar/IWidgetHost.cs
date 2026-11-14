namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>What the notification area icon needs from the widget: show it, hide it, quit.</summary>
public interface IWidgetHost
{
    /// <summary>Whether the user wants the widget on the taskbar, even while there is no taskbar to show it on.</summary>
    bool IsVisible { get; }

    void Show();

    void Hide();

    void Exit();
}
