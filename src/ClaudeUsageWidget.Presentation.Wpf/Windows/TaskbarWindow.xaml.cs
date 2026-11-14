using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Windows;

/// <summary>
/// The widget as a piece of the taskbar: a borderless, see-through window that the taskbar
/// owns and that is kept right above it in the stacking order. Being owned keeps the two
/// together through everyday activation, but not when the taskbar steps behind a full-screen
/// app or comes back from it: that is what <see cref="StayAbove"/> is for.
/// It stays a top-level window on purpose: that is the only kind WPF can make see-through
/// reliably, and the taskbar has to show between the bars and the labels.
/// </summary>
public partial class TaskbarWindow : Window
{
    // GWL_EXSTYLE.
    private const int ExtendedStyleIndex = -20;

    // WS_EX_NOACTIVATE: a click on the widget must not take the focus away from the app in use.
    private const int NoActivateStyle = 0x08000000;

    // WS_EX_TOOLWINDOW: never in Alt+Tab, and on every virtual desktop like the taskbar itself.
    private const int ToolWindowStyle = 0x00000080;

    // WM_SETTINGCHANGE: among many other things, the Windows mode went from dark to light or back.
    private const int SettingChangeMessage = 0x001A;

    // GW_HWNDPREV: the window right above the given one in the stacking order.
    private const uint PreviousWindow = 3;

    // SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE: only the stacking order changes.
    private const uint ReorderOnly = 0x0001 | 0x0002 | 0x0010;

    // HWND_TOPMOST.
    private static readonly IntPtr AboveEverything = new(-1);

    // Broadcast by Explorer every time it creates a taskbar, which includes its restarts.
    private const string TaskbarCreatedMessageName = "TaskbarCreated";

    private static readonly string AssemblyName = typeof(TaskbarWindow).Assembly.GetName().Name!;
    private static readonly Uri DarkThemeUri = ThemeUri("TaskbarDark");
    private static readonly Uri LightThemeUri = ThemeUri("TaskbarLight");

    private readonly WidgetViewModel _viewModel;
    private readonly WindowInteropHelper _interop;
    private readonly Size _naturalSize;
    private readonly int _taskbarCreatedMessage = RegisterWindowMessage(TaskbarCreatedMessageName);

    private ResourceDictionary? _theme;

    public TaskbarWindow(WidgetViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _interop = new WindowInteropHelper(this);
        _naturalSize = new Size(Slot.Width, Slot.Height);
        DataContext = viewModel;

        ApplyTheme();
    }

    public event EventHandler? HideRequested;

    public event EventHandler? ExitRequested;

    /// <summary>Explorer created a new taskbar while this window was still alive.</summary>
    public event EventHandler? TaskbarRecreated;

    /// <summary>
    /// Makes the given taskbar the owner and takes the slot next to its notification area.
    /// Safe to call again with the same taskbar: that is how the widget follows it when the
    /// notification area grows or the taskbar moves.
    /// </summary>
    public void DockTo(TaskbarGeometry taskbar)
    {
        if (_interop.Owner != taskbar.Handle)
        {
            _interop.Owner = taskbar.Handle;
        }

        // The DPI is only known once there is a native window, shown or not.
        _interop.EnsureHandle();
        var dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var slot = TaskbarSlot.For(taskbar, _naturalSize, dpiScale);

        ContentScale.ScaleX = slot.ContentScale;
        ContentScale.ScaleY = slot.ContentScale;

        Left = slot.Bounds.X / dpiScale;
        Top = slot.Bounds.Y / dpiScale;
        Width = slot.Bounds.Width / dpiScale;
        Height = slot.Bounds.Height / dpiScale;
    }

    /// <summary>
    /// Puts this window right above the taskbar, wherever the taskbar is in the stacking
    /// order. While the taskbar is always-on-top that makes this window always-on-top too;
    /// when the taskbar gives that up to sit behind a full-screen app, the window lands
    /// behind that app with it. Windows sets and clears the always-on-top flag by itself
    /// according to the neighbor the window is placed under.
    /// </summary>
    public void StayAbove(IntPtr taskbar)
    {
        var handle = _interop.Handle;
        var above = GetWindow(taskbar, PreviousWindow);

        if (handle == IntPtr.Zero || above == handle)
        {
            return;
        }

        SetWindowPos(handle, above == IntPtr.Zero ? AboveEverything : above, 0, 0, 0, 0, ReorderOnly);
    }

    protected override void OnSourceInitialized(EventArgs args)
    {
        base.OnSourceInitialized(args);

        var handle = _interop.Handle;
        SetWindowLong(
            handle,
            ExtendedStyleIndex,
            GetWindowLong(handle, ExtendedStyleIndex) | NoActivateStyle | ToolWindowStyle);

        HwndSource.FromHwnd(handle)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == SettingChangeMessage)
        {
            ApplyTheme();
        }
        else if (message == _taskbarCreatedMessage)
        {
            TaskbarRecreated?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    /// <summary>Labels, track and button states for the taskbar's current mode. A no-op when it has not changed.</summary>
    private void ApplyTheme()
    {
        var source = SystemTaskbarTheme.IsLight() ? LightThemeUri : DarkThemeUri;
        if (_theme?.Source == source)
        {
            return;
        }

        var theme = new ResourceDictionary { Source = source };
        var dictionaries = Resources.MergedDictionaries;

        if (_theme is not null)
        {
            dictionaries.Remove(_theme);
        }

        dictionaries.Add(theme);
        _theme = theme;
    }

    private void OnRefreshRequested(object sender, RoutedEventArgs args) => _viewModel.RequestRefresh();

    private void OnHideRequested(object sender, RoutedEventArgs args) => HideRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitRequested(object sender, RoutedEventArgs args) => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A menu only closes on a click elsewhere if its window is the active one, and this
    /// window never becomes active on its own. Same thing the taskbar does for its own menu.
    /// </summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs args) =>
        SetForegroundWindow(_interop.Handle);

    private static Uri ThemeUri(string name) =>
        new($"/{AssemblyName};component/Theme/{name}.xaml", UriKind.Relative);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint relation);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
    private static extern int RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}
