using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Displays;

/// <summary>
/// Un monitor conectado.
/// <see cref="PhysicalBounds"/> es su resolucion real y su posicion en el escritorio, sin
/// virtualizar por DPI: es lo que identifica el caso. <see cref="WorkArea"/> es el area sin
/// barra de tareas en coordenadas de dispositivo de este proceso: es con lo que se compara la ventana.
/// </summary>
public sealed record DisplayMonitor(Int32Rect PhysicalBounds, Int32Rect WorkArea, bool IsPrimary);
