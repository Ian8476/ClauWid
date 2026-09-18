using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Presentation.Wpf.Converters;

/// <summary>
/// Maps a severity band to its fill brush. The brushes are set in XAML from the design
/// tokens, so the palette stays in the theme and the ViewModel stays free of WPF types.
/// </summary>
public sealed class UsageSeverityBrushConverter : IValueConverter
{
    public Brush LowBrush { get; set; } = Brushes.Transparent;

    public Brush ModerateBrush { get; set; } = Brushes.Transparent;

    public Brush HighBrush { get; set; } = Brushes.Transparent;

    public Brush CriticalBrush { get; set; } = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        UsageSeverity.Moderate => ModerateBrush,
        UsageSeverity.High => HighBrush,
        UsageSeverity.Critical => CriticalBrush,
        _ => LowBrush
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
