using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using ClaudeUsageWidget.Presentation.Wpf.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Keeps the widget on the taskbar for as long as the app runs. The window itself is
/// disposable: Windows destroys it together with the taskbar that owns it, which happens
/// every time Explorer restarts. The host then waits for the new taskbar and docks a new
/// window to it, so the app's lifetime is tied to this class and not to any one window.
/// </summary>
public sealed class TaskbarWidgetHost : IWidgetHost, IDisposable
{
    // Explorer takes a few seconds to come back. Asking once a second finds its taskbar
    // soon enough, and the timer only runs while there is none.
    private static readonly TimeSpan TaskbarRetryInterval = TimeSpan.FromSeconds(1);

    private readonly WidgetViewModel _viewModel;
    private readonly ITaskbarLocator _locator;
    private readonly Dispatcher _dispatcher;
    private readonly Action _exit;
    private readonly DispatcherTimer _retryTimer;

    private TaskbarWindow? _window;
    private TaskbarGeometry? _docked;
    private IDisposable? _watch;
    private bool _isDisposed;

    public TaskbarWidgetHost(WidgetViewModel viewModel, ITaskbarLocator locator, Dispatcher dispatcher, Action exit)
    {
        _viewModel = viewModel;
        _locator = locator;
        _dispatcher = dispatcher;
        _exit = exit;

        _retryTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TaskbarRetryInterval
        };
        _retryTimer.Tick += (_, _) => Dock();
    }

    public bool IsVisible { get; private set; } = true;

    public void Start() => Dock();

    public void Show()
    {
        IsVisible = true;
        Dock();
    }

    public void Hide()
    {
        IsVisible = false;
        _window?.Hide();
    }

    public void Exit() => _exit();

    public void Dispose()
    {
        _isDisposed = true;
        _retryTimer.Stop();
        StopWatching();
        _window?.Close();
    }

    /// <summary>
    /// The single path for the first docking, for following the taskbar around and for
    /// recovering it: find the taskbar as it is now, put the widget in its slot and right
    /// above it. It runs on every change the taskbar reports, so it does nothing it can skip.
    /// </summary>
    private void Dock()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_locator.Locate() is not { } taskbar)
        {
            StopWatching();
            _window?.Hide();
            _retryTimer.Start();
            return;
        }

        _retryTimer.Stop();
        _window ??= CreateWindow();

        if (taskbar != _docked)
        {
            if (taskbar.Handle != _docked?.Handle)
            {
                StopWatching();
                _watch = _locator.Watch(taskbar, Dock);
            }

            _window.DockTo(taskbar);
            _docked = taskbar;
        }

        if (!IsVisible)
        {
            return;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.StayAbove(taskbar.Handle);
    }

    private TaskbarWindow CreateWindow()
    {
        var window = new TaskbarWindow(_viewModel);
        window.HideRequested += (_, _) => Hide();
        window.ExitRequested += (_, _) => Exit();
        window.TaskbarRecreated += (_, _) => Dock();
        window.Closed += OnWindowClosed;

        return window;
    }

    /// <summary>
    /// Nothing in the app closes this window before shutdown, so getting here earlier means
    /// its taskbar was destroyed and took it along. Docking again is queued instead of done
    /// on the spot: this runs in the middle of the window's own teardown.
    /// </summary>
    private void OnWindowClosed(object? sender, EventArgs args)
    {
        _window = null;
        StopWatching();

        if (!_isDisposed)
        {
            _dispatcher.InvokeAsync(Dock, DispatcherPriority.Background);
        }
    }

    private void StopWatching()
    {
        _watch?.Dispose();
        _watch = null;
        _docked = null;
    }
}
