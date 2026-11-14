using System.Windows;
using System.Windows.Interop;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;

namespace ClaudeUsageWidget.Tests.Support;

/// <summary>
/// A real, never shown native window standing in for the taskbar, far outside any screen so
/// the widget docked to it is not seen either. Disposing it destroys the window, and Windows
/// then destroys whatever it owns: the same thing an Explorer restart does to the widget.
/// </summary>
internal sealed class FakeTaskbar : IDisposable
{
    // WS_POPUP: a bare top-level window, no frame and no title bar.
    private const int PopupStyle = unchecked((int)0x80000000);

    private const int OffScreen = -20000;
    private const int Length = 1600;
    private const int Thickness = 60;
    private const int TrayLength = 300;

    private readonly HwndSource _window;

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

        Geometry = GeometryWithTrayOf(TrayLength);
    }

    public IntPtr Handle => _window.Handle;

    public TaskbarGeometry Geometry { get; }

    /// <summary>The same taskbar after its notification area grew or shrank.</summary>
    public TaskbarGeometry GeometryWithTrayOf(int trayLength) => new(
        Handle,
        IntPtr.Zero,
        new Int32Rect(OffScreen, OffScreen, Length, Thickness),
        new Int32Rect(OffScreen + Length - trayLength, OffScreen, trayLength, Thickness));

    public void Dispose() => _window.Dispose();
}
