namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

public interface ITaskbarLocator
{
    /// <summary>Null while there is no taskbar, which is the case while Explorer restarts.</summary>
    TaskbarGeometry? Locate();

    /// <summary>
    /// Calls <paramref name="onChanged"/> whenever the given taskbar or its notification area
    /// moves or resizes, until the returned subscription is disposed. Both the call and the
    /// dispose must happen on a thread that pumps messages.
    /// </summary>
    IDisposable Watch(TaskbarGeometry taskbar, Action onChanged);
}
