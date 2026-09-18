using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeUsageWidget.Presentation.Wpf.Controls;

/// <summary>
/// Una barra del widget: carril, relleno animado y las dos etiquetas.
/// Su unica responsabilidad es geometria y pintura. No sabe nada de Claude,
/// de ventanas de limite ni de relojes: recibe una fraccion ya calculada.
/// </summary>
public partial class UsageBar : UserControl
{
    private static readonly Duration FillDuration = new(TimeSpan.FromMilliseconds(420));

    // Owned by the control, never frozen, so its color can be animated between bands.
    private readonly SolidColorBrush _fillPaint = new(Colors.Transparent);

    public UsageBar()
    {
        InitializeComponent();
        Fill.Background = _fillPaint;
        Track.SizeChanged += (_, _) => ApplyFill(animate: false);
    }

    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(UsageBar),
        new PropertyMetadata(0d, OnFractionChanged));

    public static readonly DependencyProperty ResetLabelProperty = DependencyProperty.Register(
        nameof(ResetLabel), typeof(string), typeof(UsageBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PercentLabelProperty = DependencyProperty.Register(
        nameof(PercentLabel), typeof(string), typeof(UsageBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(UsageBar),
        new PropertyMetadata(Brushes.Transparent, OnFillBrushChanged));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(UsageBar), new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
        nameof(LabelBrush), typeof(Brush), typeof(UsageBar), new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty PercentLabelBrushProperty = DependencyProperty.Register(
        nameof(PercentLabelBrush), typeof(Brush), typeof(UsageBar), new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty LabelFontFamilyProperty = DependencyProperty.Register(
        nameof(LabelFontFamily), typeof(FontFamily), typeof(UsageBar), new PropertyMetadata(default(FontFamily)));

    public static readonly DependencyProperty LabelFontSizeProperty = DependencyProperty.Register(
        nameof(LabelFontSize), typeof(double), typeof(UsageBar), new PropertyMetadata(13d));

    public static readonly DependencyProperty LabelFontWeightProperty = DependencyProperty.Register(
        nameof(LabelFontWeight), typeof(FontWeight), typeof(UsageBar), new PropertyMetadata(FontWeights.Normal));

    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight), typeof(double), typeof(UsageBar), new PropertyMetadata(6d));

    public static readonly DependencyProperty BarCornerRadiusProperty = DependencyProperty.Register(
        nameof(BarCornerRadius), typeof(CornerRadius), typeof(UsageBar),
        new PropertyMetadata(new CornerRadius(3)));

    public static readonly DependencyProperty LabelSpacingProperty = DependencyProperty.Register(
        nameof(LabelSpacing), typeof(Thickness), typeof(UsageBar),
        new PropertyMetadata(new Thickness(0, 0, 0, 6)));

    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public string ResetLabel
    {
        get => (string)GetValue(ResetLabelProperty);
        set => SetValue(ResetLabelProperty, value);
    }

    public string PercentLabel
    {
        get => (string)GetValue(PercentLabelProperty);
        set => SetValue(PercentLabelProperty, value);
    }

    public Brush FillBrush
    {
        get => (Brush)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush LabelBrush
    {
        get => (Brush)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public Brush PercentLabelBrush
    {
        get => (Brush)GetValue(PercentLabelBrushProperty);
        set => SetValue(PercentLabelBrushProperty, value);
    }

    public FontFamily LabelFontFamily
    {
        get => (FontFamily)GetValue(LabelFontFamilyProperty);
        set => SetValue(LabelFontFamilyProperty, value);
    }

    public double LabelFontSize
    {
        get => (double)GetValue(LabelFontSizeProperty);
        set => SetValue(LabelFontSizeProperty, value);
    }

    public FontWeight LabelFontWeight
    {
        get => (FontWeight)GetValue(LabelFontWeightProperty);
        set => SetValue(LabelFontWeightProperty, value);
    }

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public CornerRadius BarCornerRadius
    {
        get => (CornerRadius)GetValue(BarCornerRadiusProperty);
        set => SetValue(BarCornerRadiusProperty, value);
    }

    public Thickness LabelSpacing
    {
        get => (Thickness)GetValue(LabelSpacingProperty);
        set => SetValue(LabelSpacingProperty, value);
    }

    private static void OnFractionChanged(DependencyObject source, DependencyPropertyChangedEventArgs args) =>
        ((UsageBar)source).ApplyFill(animate: true);

    private static void OnFillBrushChanged(DependencyObject source, DependencyPropertyChangedEventArgs args) =>
        ((UsageBar)source).ApplyFillBrush();

    /// <summary>
    /// A solid brush fades into place alongside the width animation, so crossing into
    /// another band reads as one motion instead of a flash. Anything else is shown as is.
    /// </summary>
    private void ApplyFillBrush()
    {
        if (FillBrush is not SolidColorBrush { Color: var color })
        {
            Fill.Background = FillBrush;
            return;
        }

        Fill.Background = _fillPaint;
        _fillPaint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, FillDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>
    /// El ancho se calcula a mano en vez de con columnas proporcionales porque el
    /// relleno necesita un minimo: con una fraccion muy baja, un pill de 2px de ancho
    /// y 10px de radio se dibuja como un artefacto, no como una barra.
    /// </summary>
    private void ApplyFill(bool animate)
    {
        var available = Track.ActualWidth;
        if (available <= 0d)
        {
            return;
        }

        var fraction = double.IsNaN(Fraction) ? 0d : Math.Clamp(Fraction, 0d, 1d);
        var target = fraction <= 0d
            ? 0d
            : Math.Clamp(fraction * available, BarHeight, available);

        if (!animate)
        {
            Fill.BeginAnimation(FrameworkElement.WidthProperty, null);
            Fill.Width = target;
            return;
        }

        var animation = new DoubleAnimation(target, FillDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Fill.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }
}
