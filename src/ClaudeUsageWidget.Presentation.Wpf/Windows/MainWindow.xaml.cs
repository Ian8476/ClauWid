using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Displays;
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

    // Trozo minimo de la ventana, en DIPs, que tiene que caer dentro de un monitor para
    // poder verla y arrastrarla. Con menos se da por perdida y se recoloca.
    private const double MinimumReachableSize = 48d;

    // WM_EXITSIZEMOVE: Windows lo envia al terminar de mover o redimensionar, DragMove incluido.
    private const int ExitSizeMoveMessage = 0x0232;

    // WM_DISPLAYCHANGE: se conecto o desconecto un monitor, o cambio una resolucion.
    private const int DisplayChangeMessage = 0x007E;

    private readonly IWidgetPlacementStore _placementStore;
    private readonly IDisplayLayoutProvider _displayLayoutProvider;
    private readonly double _defaultWidth;

    // Caso de monitores al que corresponde la posicion actual de la ventana.
    private string _layoutKey = string.Empty;

    public MainWindow(
        WidgetViewModel viewModel,
        IWidgetPlacementStore placementStore,
        IDisplayLayoutProvider displayLayoutProvider)
    {
        InitializeComponent();

        _placementStore = placementStore;
        _displayLayoutProvider = displayLayoutProvider;
        _defaultWidth = Width;
        MinWidth = MinimumContentWidth * MinimumScale;
        DataContext = viewModel;

        SizeChanged += (_, _) => ApplyContentScale();
        Loaded += (_, _) => EnsureReachable();

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
    /// Un cambio de monitores llega mientras Windows todavia recoloca ventanas, y suele venir
    /// en rafaga: se atiende cuando el dispatcher queda libre.
    /// </summary>
    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case ExitSizeMoveMessage:
                SavePlacement();
                break;

            case DisplayChangeMessage:
                Dispatcher.InvokeAsync(OnDisplayLayoutChanged, DispatcherPriority.Background);
                break;
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

    private void OnHideRequested(object sender, RoutedEventArgs args) => Hide();

    private void OnCloseRequested(object sender, RoutedEventArgs args) => Close();

    private void OnResetSizeRequested(object sender, RoutedEventArgs args)
    {
        ResetSize();
        SavePlacement();
    }

    /// <summary>
    /// Al conectar o desconectar un monitor, la ventana salta a la posicion recordada para el
    /// nuevo caso. Si el caso es nuevo se queda donde este, siempre que siga a la vista.
    /// </summary>
    private void OnDisplayLayoutChanged()
    {
        var layoutKey = _displayLayoutProvider.GetCurrent().Key;
        if (layoutKey != _layoutKey)
        {
            _layoutKey = layoutKey;

            if (_placementStore.Load(layoutKey) is { } placement)
            {
                ApplyPlacement(placement);
                UpdateLayout();
            }
        }

        EnsureReachable();
    }

    /// <summary>
    /// La escala vuelve a 1 antes de reactivar SizeToContent: si no, la ventana se ajustaria
    /// al contenido ya ampliado y el zoom se mantendria a si mismo.
    /// </summary>
    private void ResetSize()
    {
        ContentScale.ScaleX = 1d;
        ContentScale.ScaleY = 1d;
        Width = _defaultWidth;
        SizeToContent = SizeToContent.Height;
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
    /// Se guarda bajo el caso de monitores conectado ahora, sin tocar los demas.
    /// El tamano solo se guarda si el usuario lo cambio: WPF pasa SizeToContent a Manual al
    /// redimensionar. Asi un cambio futuro de tipografia no queda tapado por un alto viejo.
    /// </summary>
    private void SavePlacement()
    {
        _layoutKey = _displayLayoutProvider.GetCurrent().Key;

        _placementStore.Save(_layoutKey, SizeToContent is SizeToContent.Manual
            ? new WidgetPlacement(Left, Top, ActualWidth, ActualHeight)
            : new WidgetPlacement(Left, Top));
    }

    private void RestorePlacement()
    {
        _layoutKey = _displayLayoutProvider.GetCurrent().Key;

        if (_placementStore.Load(_layoutKey) is not { } placement)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        ApplyPlacement(placement);
    }

    private void ApplyPlacement(WidgetPlacement placement)
    {
        Left = placement.Left;
        Top = placement.Top;

        if (placement is { Width: { } width, Height: { } height })
        {
            SizeToContent = SizeToContent.Manual;
            Width = width;
            Height = height;
            return;
        }

        ResetSize();
    }

    /// <summary>
    /// Red de seguridad para cuando la posicion del caso no sirve aunque los monitores
    /// coincidan: otra escala de pantalla, un monitor cambiado de lado o un placement.json
    /// editado a mano. Si no queda a la vista un trozo suficiente para agarrarla, se centra
    /// en el monitor principal. No se guarda: la posicion de un caso solo la decide el usuario.
    /// </summary>
    private void EnsureReachable()
    {
        if (PresentationSource.FromVisual(this)?.CompositionTarget is not { } target)
        {
            return;
        }

        var workAreas = _displayLayoutProvider.GetCurrent().Monitors
            .Select(monitor => ToDips(monitor.WorkArea, target.TransformFromDevice))
            .ToList();

        if (workAreas.Count == 0)
        {
            return;
        }

        var bounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        var minimumWidth = Math.Min(MinimumReachableSize, bounds.Width);
        var minimumHeight = Math.Min(MinimumReachableSize, bounds.Height);

        var isReachable = workAreas.Any(area =>
            Rect.Intersect(bounds, area) is { IsEmpty: false } overlap
            && overlap.Width >= minimumWidth
            && overlap.Height >= minimumHeight);

        if (isReachable)
        {
            return;
        }

        // Monitors pone el principal primero.
        var primary = workAreas[0];
        Left = primary.Left + (primary.Width - bounds.Width) / 2d;
        Top = primary.Top + (primary.Height - bounds.Height) / 2d;
    }

    private static Rect ToDips(Int32Rect area, Matrix fromDevice) =>
        Rect.Transform(new Rect(area.X, area.Y, area.Width, area.Height), fromDevice);
}
