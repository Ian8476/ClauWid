using System.ComponentModel;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// Keeps the widget on the taskbar for as long as the app runs. The window itself is
/// disposable: Windows destroys it together with the taskbar it is a child of, which happens
/// every time Explorer restarts. The host then waits for the new taskbar and creates a new
/// window in it, so the app's lifetime is tied to this class and not to any one window.
/// </summary>
public sealed class TaskbarWidgetHost : IWidgetHost, IDisposable
{
    // Explorer takes a few seconds to come back, and other apps' widgets next to the
    // notification area come, go and resize in their own processes, where none of the hooks on
    // Explorer's thread can see them. Looking again every couple of seconds finds both soon
    // enough, and a look is a window lookup and a walk over the taskbar's children.
    private static readonly TimeSpan DockInterval = TimeSpan.FromSeconds(2);

    private readonly WidgetViewModel _viewModel;
    private readonly ITaskbarLocator _locator;
    private readonly Dispatcher _dispatcher;
    private readonly Action _exit;
    private readonly DispatcherTimer _dockTimer;

    private TaskbarChildWindow? _child;
    private TaskbarGeometry? _docked;
    private IDisposable? _watch;
    private bool _isDisposed;

    public TaskbarWidgetHost(WidgetViewModel viewModel, ITaskbarLocator locator, Dispatcher dispatcher, Action exit)
    {
        _viewModel = viewModel;
        _locator = locator;
        _dispatcher = dispatcher;
        _exit = exit;

        _dockTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = DockInterval
        };
        _dockTimer.Tick += (_, _) => Dock();
    }

    public bool IsVisible { get; private set; } = true;

    public void Start()
    {
        _dockTimer.Start();
        Dock();
    }

    public void Show()
    {
        IsVisible = true;
        Dock();
    }

    public void Hide()
    {
        IsVisible = false;
        _child?.Hide();
    }

    public void Exit() => _exit();

    public void Dispose()
    {
        _isDisposed = true;
        _dockTimer.Stop();
        StopWatching();
        DiscardChild();
    }

    /// <summary>
    /// The single path for the first docking, for following the taskbar around and for
    /// recovering it: find the taskbar as it is now, put the widget in its slot and above the
    /// taskbar's own content. It runs on every change the taskbar reports and on a timer, so it
    /// does nothing it can skip: the same geometry as last time is not placed again.
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
            _child?.Hide();
            return;
        }

        // A window cannot be moved to the child list of another taskbar: a new one is made.
        if (_child is not null && _child.Taskbar != taskbar.Handle)
        {
            StopWatching();
            DiscardChild();
        }

        if ((_child ??= CreateChild(taskbar)) is not { } child)
        {
            return;
        }

        if (taskbar != _docked)
        {
            if (taskbar.Handle != _docked?.Handle)
            {
                StopWatching();
                _watch = _locator.Watch(taskbar, Dock);
            }

            child.Place(TaskbarSlot.For(taskbar, child.NaturalSize, child.DpiScale));
            _docked = taskbar;
        }

        if (!IsVisible)
        {
            return;
        }

        if (!child.IsVisible)
        {
            child.Show();
        }

        child.EnsureAboveTaskbarContent();
    }

    /// <summary>
    /// Null when the taskbar went away between being found and being used, which is Explorer
    /// shutting down: the next look finds the new one. Any other failure to create the window
    /// is a real problem and is not swallowed.
    /// </summary>
    private TaskbarChildWindow? CreateChild(TaskbarGeometry taskbar)
    {
        TaskbarChildWindow child;

        try
        {
            child = new TaskbarChildWindow(taskbar.Handle, _viewModel);
        }
        catch (Win32Exception) when (_locator.Locate()?.Handle != taskbar.Handle)
        {
            return null;
        }

        child.HideRequested += (_, _) => Hide();
        child.ExitRequested += (_, _) => Exit();
        child.Destroyed += OnChildDestroyed;

        return child;
    }

    /// <summary>
    /// Nothing in the app destroys this window before shutdown, so getting here earlier means
    /// its taskbar was destroyed and took it along. Docking again is queued instead of done
    /// on the spot: this runs in the middle of the window's own teardown.
    /// </summary>
    private void OnChildDestroyed(object? sender, EventArgs args)
    {
        _child = null;
        StopWatching();

        if (!_isDisposed)
        {
            _dispatcher.InvokeAsync(Dock, DispatcherPriority.Background);
        }
    }

    private void DiscardChild()
    {
        _child?.Dispose();
        _child = null;
    }

    private void StopWatching()
    {
        _watch?.Dispose();
        _watch = null;
        _docked = null;
    }
}
