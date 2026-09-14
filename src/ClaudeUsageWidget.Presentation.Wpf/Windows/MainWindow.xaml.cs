using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClaudeUsageWidget.Presentation.Wpf.Persistence;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Windows;

public partial class MainWindow : Window
{
    // Rango de zoom al redimensionar. Por debajo de 0.7 las etiquetas dejan de leerse
    // y por encima de 3 el widget deja de ser un widget.
    private const double MinimumScale = 0.7d;
    private const double MaximumScale = 3d;

    // Ancho sin escalar, margenes incluidos, por debajo del cual las dos etiquetas de una barra se pisan.
    private const double MinimumContentWidth = 300d;

    // WM_EXITSIZEMOVE: Windows lo envia al terminar de mover o redimensionar, DragMove incluido.
    private const int ExitSizeMoveMessage = 0x0232;

    private readonly IWidgetPlacementStore _placementStore;
    private readonly double _defaultWidth;

    public MainWindow(WidgetViewModel viewModel, IWidgetPlacementStore placementStore)
    {
        InitializeComponent();

        _placementStore = placementStore;
        _defaultWidth = Width;
        MinWidth = MinimumContentWidth * MinimumScale;
        DataContext = viewModel;

        SizeChanged += (_, _) => ApplyContentScale();

        RestorePlacement();
    }

    protected override void OnSourceInitialized(EventArgs args)
    {
        base.OnSourceInitialized(args);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(OnWindowMessage);
    }

    /// <summary>
    /// Mover y redimensionar acaban en el mismo mensaje, asi que hay un unico punto de
    /// guardado y una sola escritura por gesto, en vez de una por cada pixel arrastrado.
    /// </summary>
    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == ExitSizeMoveMessage)
        {
            SavePlacement();
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Sin barra de titulo, la superficie completa es el asa de arrastre.
    /// DragMove lanza InvalidOperationException si el boton ya se solto, de ahi la guarda.
    /// </summary>
    private void OnSurfaceDragStarted(object sender, MouseButtonEventArgs args)
    {
        if (args.ButtonState is not MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void OnCloseRequested(object sender, RoutedEventArgs args) => Close();

    /// <summary>
    /// La escala vuelve a 1 antes de reactivar SizeToContent: si no, la ventana se ajustaria
    /// al contenido ya ampliado y el zoom se mantendria a si mismo.
    /// </summary>
    private void OnResetSizeRequested(object sender, RoutedEventArgs args)
    {
        ContentScale.ScaleX = 1d;
        ContentScale.ScaleY = 1d;
        Width = _defaultWidth;
        SizeToContent = SizeToContent.Height;

        SavePlacement();
    }

    /// <summary>
    /// El alto decide el zoom y el ancho sobrante alarga las barras: agrandar la ventana
    /// agranda el texto, ensancharla solo estira las barras. El zoom nunca supera lo que el
    /// ancho permite sin que las etiquetas se pisen; si sobra alto, las filas quedan centradas
    /// en vez de ensanchar la ventana por su cuenta mientras se arrastra otro borde.
    /// El alto natural no depende del ancho porque cada etiqueta ocupa una sola linea.
    /// </summary>
    private void ApplyContentScale()
    {
        var naturalHeight = Rows.DesiredSize.Height + Surface.Padding.Top + Surface.Padding.Bottom;
        if (naturalHeight <= 0d)
        {
            return;
        }

        var scaleByHeight = ActualHeight / naturalHeight;
        var scaleByWidth = ActualWidth / MinimumContentWidth;
        var scale = Math.Clamp(Math.Min(scaleByHeight, scaleByWidth), MinimumScale, MaximumScale);

        ContentScale.ScaleX = scale;
        ContentScale.ScaleY = scale;

        MinHeight = naturalHeight * MinimumScale;
        MaxHeight = naturalHeight * MaximumScale;
    }

    /// <summary>
    /// El tamano solo se guarda si el usuario lo cambio: WPF pasa SizeToContent a Manual al
    /// redimensionar. Asi un cambio futuro de tipografia no queda tapado por un alto viejo.
    /// </summary>
    private void SavePlacement() =>
        _placementStore.Save(SizeToContent is SizeToContent.Manual
            ? new WidgetPlacement(Left, Top, ActualWidth, ActualHeight)
            : new WidgetPlacement(Left, Top));

    private void RestorePlacement()
    {
        if (_placementStore.Load() is not { } placement)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;

        if (placement is { Width: { } width, Height: { } height })
        {
            SizeToContent = SizeToContent.Manual;
            Width = width;
            Height = height;
        }
    }
}
