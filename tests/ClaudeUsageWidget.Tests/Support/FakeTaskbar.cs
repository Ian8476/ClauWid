using System.Windows;
using System.Windows.Interop;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;

namespace ClaudeUsageWidget.Tests.Support;

/// <summary>
/// A real, never shown native window standing in for the taskbar, far outside any screen so
/// the widget docked to it is not seen either. Disposing it destroys the window, and Windows
/// then destroys what it holds: the same thing an Explorer restart does to the widget.
/// </summary>
internal sealed class FakeTaskbar : IDisposable
{
    // WS_POPUP: a bare top-level window, no frame and no title bar.
    private const int PopupStyle = unchecked((int)0x80000000);

    // WS_CHILD | WS_VISIBLE.
    private const int VisibleChildStyle = 0x40000000 | 0x10000000;

    private const int OffScreen = -20000;
    private const int Length = 1600;
    private const int Thickness = 60;
    private const int TrayLength = 300;

    private readonly HwndSource _window;
    private readonly List<HwndSource> _content = [];
    private readonly HashSet<int> _received = [];

    public FakeTaskbar()
    {
        _window = new HwndSource(new HwndSourceParameters(nameof(FakeTaskbar))
        {
            WindowStyle = PopupStyle,
            PositionX = OffScreen,
            PositionY = OffScreen,
            Width = Length,
            Height = Thickness
        });

        _window.AddHook(RecordMessage);
        Geometry = GeometryWithTrayOf(TrayLength);
    }

    public IntPtr Handle => _window.Handle;

    public TaskbarGeometry Geometry { get; }

    /// <summary>Whether the taskbar's own window procedure has been sent this message, by anyone, itself or a child.</summary>
    public bool Received(int message) => _received.Contains(message);

    /// <summary>The same taskbar after its notification area grew or shrank.</summary>
    public TaskbarGeometry GeometryWithTrayOf(int trayLength) => new(
        Handle,
        IntPtr.Zero,
        new Int32Rect(OffScreen, OffScreen, Length, Thickness),
        new Int32Rect(OffScreen + Length - trayLength, OffScreen, trayLength, Thickness));

    /// <summary>
    /// A child that covers the whole taskbar, the way Explorer's own XAML surface does. Windows
    /// puts every new child at the bottom of the order, so a widget made after this one starts under it.
    /// </summary>
    public IntPtr AddContentChild()
    {
        var content = new HwndSource(new HwndSourceParameters(nameof(AddContentChild))
        {
            ParentWindow = Handle,
            WindowStyle = VisibleChildStyle,
            PositionX = 0,
            PositionY = 0,
            Width = Length,
            Height = Thickness
        });

        _content.Add(content);
        return content.Handle;
    }

    private IntPtr RecordMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        _received.Add(message);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _window.Dispose();

        foreach (var content in _content)
        {
            content.Dispose();
        }
    }
}
