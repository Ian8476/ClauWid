namespace ClaudeUsageWidget.Presentation.Wpf.Persistence;

/// <summary>
/// Posicion y, si el usuario redimensiono, tamano. Width y Height son opcionales para que
/// un placement.json anterior, que solo tenia Left y Top, se siga leyendo.
/// </summary>
public sealed record WidgetPlacement(double Left, double Top, double? Width = null, double? Height = null);
