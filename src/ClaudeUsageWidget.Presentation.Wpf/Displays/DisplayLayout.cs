using System.Globalization;

namespace ClaudeUsageWidget.Presentation.Wpf.Displays;

/// <summary>
/// Los monitores conectados en un momento dado. Su <see cref="Key"/> identifica el caso para
/// recordar una posicion distinta por combinacion: "1920x1080" con un monitor,
/// "2560x1440 + 1920x1080@2560,0" con dos, "3840x2160" con uno 4K.
/// </summary>
public sealed class DisplayLayout
{
    private const string MonitorSeparator = " + ";

    public DisplayLayout(IEnumerable<DisplayMonitor> monitors)
    {
        // El principal primero y el resto de izquierda a derecha: la clave no depende del
        // orden en que Windows enumere los monitores.
        Monitors = monitors
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ThenBy(monitor => monitor.PhysicalBounds.X)
            .ThenBy(monitor => monitor.PhysicalBounds.Y)
            .ToList();

        Key = string.Join(MonitorSeparator, Monitors.Select(FormatMonitor));
    }

    /// <summary>El principal, si Windows informo alguno, va siempre primero.</summary>
    public IReadOnlyList<DisplayMonitor> Monitors { get; }

    public string Key { get; }

    /// <summary>
    /// El principal siempre esta en 0,0, asi que solo el resto lleva posicion: el mismo
    /// monitor a la izquierda o a la derecha son casos distintos, porque las coordenadas cambian.
    /// </summary>
    private static string FormatMonitor(DisplayMonitor monitor)
    {
        var bounds = monitor.PhysicalBounds;

        return monitor.IsPrimary
            ? string.Create(CultureInfo.InvariantCulture, $"{bounds.Width}x{bounds.Height}")
            : string.Create(CultureInfo.InvariantCulture, $"{bounds.Width}x{bounds.Height}@{bounds.X},{bounds.Y}");
    }
}
