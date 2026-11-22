using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeUsageWidget.Presentation.Wpf.Controls;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using Microsoft.Win32;

namespace ClaudeUsageWidget.Presentation.Wpf.Taskbar;

/// <summary>
/// The widget as a real part of the taskbar: a native child window of it, hosting
/// <see cref="TaskbarWidgetView"/>. Being a child is what makes the widget move, hide and
/// stack with the taskbar without any of it being tracked from here, which a top-level window
/// that merely sits over the taskbar cannot do.
/// Windows destroys the window together with the taskbar, which happens every time Explorer
/// restarts: <see cref="Destroyed"/> reports it, and the owner creates a new one.
/// </summary>
public sealed class TaskbarChildWindow : IDisposable
{
    private const string WindowName = "ClaudeUsageWidget";

    // WS_CHILD: lives inside the taskbar and is clipped to it.
    private const int ChildStyle = 0x40000000;

    // WS_CLIPSIBLINGS: does not paint over the other children of the taskbar, and they do not
    // paint over it, whichever order they end up in.
    private const int ClipSiblingsStyle = 0x04000000;

    // WS_EX_NOACTIVATE: a click on the widget must not take the focus away from the app in use.
    private const int NoActivateStyle = 0x08000000;

    // WS_VISIBLE, read back from GWL_STYLE to know whether the widget itself is shown. The
    // IsWindowVisible function would also be false while the taskbar is, which is not the question.
    private const int VisibleStyle = 0x10000000;

    // GWL_STYLE.
    private const int StyleIndex = -16;

    // SW_HIDE, and SW_SHOWNA: show without activating, which a child must not do anyway.
    private const int HideCommand = 0;
    private const int ShowNoActivateCommand = 8;

    // GW_CHILD: the topmost child of a window. GW_HWNDNEXT: the sibling right below a window.
    private const uint FirstChild = 5;
    private const uint NextSibling = 2;

    // SWP_NOZORDER | SWP_NOACTIVATE: only the position and size change, nothing else.
    private const uint MoveAndResize = 0x0004 | 0x0010;

    // SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE: only the stacking order changes.
    private const uint ReorderOnly = 0x0001 | 0x0002 | 0x0010;

    // The window is created hidden and unplaced; Place gives it its real rectangle.
    private const int InitialSize = 1;

    // WM_MOUSEACTIVATE: a mouse button went down on an inactive window, asking whether to
    // activate it. MA_NOACTIVATE is the answer "no, and let the click through".
    private const int MouseActivateMessage = 0x0021;
    private static readonly IntPtr NoActivateAnswer = new(3);

    // WM_CONTEXTMENU: the user asked for a context menu on the window.
    private const int ContextMenuMessage = 0x007B;

    // HWND_TOP: above all the other children.
    private static readonly IntPtr AboveSiblings = IntPtr.Zero;

    private readonly WidgetViewModel _viewModel;
    private readonly TaskbarWidgetView _view;
    private readonly TaskbarMenu _menu;
    private readonly HwndSource _source;
    private readonly uint _taskbarProcess;

    private Placement? _placed;
    private bool _isDisposed;

    /// <summary>
    /// The window is layered with per-pixel alpha, so the taskbar shows through everything the
    /// view does not paint. That is only possible for a child window when the app manifest
    /// declares Windows 10 support.
    /// </summary>
    /// <param name="taskbar">The taskbar window this one becomes a child of.</param>
    public TaskbarChildWindow(IntPtr taskbar, WidgetViewModel viewModel)
    {
        Taskbar = taskbar;
        GetWindowThreadProcessId(taskbar, out _taskbarProcess);

        _viewModel = viewModel;
        _view = new TaskbarWidgetView(viewModel);
        _view.MenuRequested += OnMenuRequested;

        _source = new HwndSource(new HwndSourceParameters(WindowName)
        {
            ParentWindow = taskbar,
            WindowStyle = ChildStyle | ClipSiblingsStyle,
            ExtendedWindowStyle = NoActivateStyle,
            PositionX = 0,
            PositionY = 0,
            Width = InitialSize,
            Height = InitialSize,
            UsesPerPixelTransparency = true
        });

        // Made after the window, which is the part that can fail when the taskbar is going
        // away, so a failure there leaves no menu window behind.
        try
        {
            _menu = new TaskbarMenu();
        }
        catch
        {
            _source.Dispose();
            throw;
        }

        _source.CompositionTarget.BackgroundColor = Colors.Transparent;
        _source.RootVisual = _view;
        _source.AddHook(OnWindowMessage);
        _source.Disposed += OnSourceDisposed;

        // A broadcast WM_SETTINGCHANGE only reaches top-level windows, so a child never gets
        // it. This static event is the other way to hear that the Windows mode changed.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? HideRequested;

    public event EventHandler? ExitRequested;

    /// <summary>
    /// The window went away without <see cref="Dispose"/> being called, which is what happens
    /// when Explorer destroys the taskbar. Raised in the middle of the window's own teardown.
    /// </summary>
    public event EventHandler? Destroyed;

    /// <summary>The taskbar window this one is a child of.</summary>
    public IntPtr Taskbar { get; }

    /// <summary>Size of the content at zoom 1, in device-independent units.</summary>
    public Size NaturalSize => _view.NaturalSize;

    /// <summary>Device pixels per device-independent unit, as of the display the window is on.</summary>
    public double DpiScale => _source.CompositionTarget.TransformToDevice.M11;

    /// <summary>Whether the widget itself is shown, whatever the state of the taskbar it is in.</summary>
    public bool IsVisible => !_isDisposed && (GetWindowLong(_source.Handle, StyleIndex) & VisibleStyle) != 0;

    /// <summary>
    /// Puts the window in the slot, which is in screen pixels, and zooms the content to fit it.
    /// Safe to call again with the same slot, and cheap then: that is how it is called on a timer.
    /// </summary>
    public void Place(TaskbarSlot slot)
    {
        if (_isDisposed)
        {
            return;
        }

        var target = ToTaskbarClient(slot);
        if (target == _placed)
        {
            return;
        }

        _view.SetContentScale(target.ContentScale);
        SetWindowPos(_source.Handle, IntPtr.Zero, target.X, target.Y, target.Width, target.Height, MoveAndResize);
        _placed = target;
    }

    /// <summary>
    /// Makes sure the widget is not under Explorer's own content. The taskbar's XAML surface is
    /// a child of it that covers it entirely, and anything below that child is invisible.
    /// Only that child is cleared: the widget goes right above the topmost child that belongs
    /// to the taskbar's process, and stays below whatever other apps' widgets (foreign children
    /// too) are above it. Forcing it to the very top instead would make two apps that both
    /// want the top raise each other forever, each reacting to the other's reorder.
    /// Does nothing, without touching the window, when the order is already right.
    /// </summary>
    public void EnsureAboveTaskbarContent()
    {
        if (_isDisposed)
        {
            return;
        }

        var self = _source.Handle;
        var previous = IntPtr.Zero;

        for (var child = GetWindow(Taskbar, FirstChild); child != IntPtr.Zero; child = GetWindow(child, NextSibling))
        {
            if (child == self)
            {
                // Met before any of Explorer's windows: already above all of them.
                return;
            }

            GetWindowThreadProcessId(child, out var process);
            if (process == _taskbarProcess)
            {
                SetWindowPos(self, previous == IntPtr.Zero ? AboveSiblings : previous, 0, 0, 0, 0, ReorderOnly);
                return;
            }

            previous = child;
        }
    }

    /// <summary>Shown without taking the focus. Safe to call when it is already shown.</summary>
    public void Show()
    {
        if (!_isDisposed)
        {
            ShowWindow(_source.Handle, ShowNoActivateCommand);
        }
    }

    public void Hide()
    {
        if (!_isDisposed)
        {
            ShowWindow(_source.Handle, HideCommand);
        }
    }

    /// <summary>Destroys the window. <see cref="Destroyed"/> is not raised for this.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Release();
        _source.Disposed -= OnSourceDisposed;
        _source.Dispose();
    }

    /// <summary>
    /// WS_EX_NOACTIVATE on a child does not cover its parent: a click on the widget sends
    /// WM_MOUSEACTIVATE to it, DefWindowProc passes that up, and the taskbar answers by
    /// activating itself. That takes the focus from the app the user is working in, and it
    /// closes the context menu right after it opens. Answering it here stops the chain, while
    /// the click itself is still delivered to the widget.
    /// WM_CONTEXTMENU is swallowed for a similar reason: left alone, DefWindowProc of a child
    /// forwards it to the parent, and the taskbar must never see it. It would open its own
    /// menu on top of ours. The right click is already handled as a mouse event by the view.
    /// The hook is held by the HwndSource alone and goes away with it, so it needs no removal.
    /// </summary>
    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case MouseActivateMessage:
                handled = true;
                return NoActivateAnswer;

            case ContextMenuMessage:
                handled = true;
                return IntPtr.Zero;

            default:
                return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Opens the native menu at the mouse and does what the user picks. The menu waits for
    /// its own close, so the window can have been destroyed by the time it returns.
    /// </summary>
    private void OnMenuRequested(object? sender, EventArgs args)
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var command = _menu.ShowAt(new Point(cursor.X, cursor.Y));
        if (_isDisposed)
        {
            return;
        }

        switch (command)
        {
            case MenuCommand.Refresh:
                _viewModel.RequestRefresh();
                break;

            case MenuCommand.Hide:
                HideRequested?.Invoke(this, EventArgs.Empty);
                break;

            case MenuCommand.Exit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnSourceDisposed(object? sender, EventArgs args)
    {
        Release();
        Destroyed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What has to be let go of however the window ends: the static event keeps this object alive.</summary>
    private void Release()
    {
        _isDisposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _menu.Dispose();
    }

    /// <summary>Raised on a thread of SystemEvents' own, and the view belongs to the UI thread.</summary>
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs args) =>
        _view.Dispatcher.BeginInvoke(() =>
        {
            if (!_isDisposed)
            {
                _view.ApplyTheme();
            }
        });

    /// <summary>
    /// A child's position is relative to its parent's client area, while the slot is in screen
    /// pixels. Mapping both corners, instead of one corner and the size, also gives the right
    /// rectangle when the parent is mirrored and the horizontal axis runs backwards.
    /// The result is compared with the previous one to skip calls that would change nothing.
    /// </summary>
    private Placement ToTaskbarClient(TaskbarSlot slot)
    {
        var screen = slot.Bounds;
        var corners = new[]
        {
            new NativePoint(screen.X, screen.Y),
            new NativePoint(screen.X + screen.Width, screen.Y + screen.Height)
        };

        MapWindowPoints(IntPtr.Zero, Taskbar, corners, corners.Length);

        return new Placement(
            Math.Min(corners[0].X, corners[1].X),
            Math.Min(corners[0].Y, corners[1].Y),
            Math.Abs(corners[1].X - corners[0].X),
            Math.Abs(corners[1].Y - corners[0].Y),
            slot.ContentScale);
    }

    /// <summary>The slot in the taskbar's client coordinates, with the zoom that goes with it.</summary>
    private readonly record struct Placement(int X, int Y, int Width, int Height, double ContentScale);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint relation);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern int MapWindowPoints(IntPtr from, IntPtr to, [In, Out] NativePoint[] points, int count);
}
