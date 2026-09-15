using System.Globalization;
using System.Windows.Threading;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;
using ClaudeUsageWidget.Infrastructure.ClaudeCode;
using ClaudeUsageWidget.Infrastructure.Time;
using ClaudeUsageWidget.Presentation.Wpf.Displays;
using ClaudeUsageWidget.Presentation.Wpf.Persistence;
using ClaudeUsageWidget.Presentation.Wpf.Presenters;
using ClaudeUsageWidget.Presentation.Wpf.Tray;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using ClaudeUsageWidget.Presentation.Wpf.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Composition;

/// <summary>
/// Unico punto donde se decide que implementacion concreta usa cada puerto.
/// Volver al simulador (Infrastructure.Fake) para iterar la UI es cambiar la linea del proveedor.
/// No hay contenedor de inyeccion porque a esta escala no aporta nada: todo lo demas
/// ya recibe sus dependencias por constructor, asi que introducirlo despues es aditivo.
/// </summary>
public sealed class WidgetCompositionRoot : IDisposable
{
    // Cada lectura es una llamada de red autenticada: consultar mas a menudo no aporta
    // precision visible y arriesga respuestas 429.
    private static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CountdownInterval = TimeSpan.FromSeconds(30);

    private ClaudeOAuthUsageProvider? _provider;
    private UsageWidgetPresenter? _presenter;
    private WidgetTrayIcon? _trayIcon;

    public MainWindow CreateMainWindow(Dispatcher dispatcher)
    {
        IClock clock = new SystemClock();

        _provider = new ClaudeOAuthUsageProvider(
            new ClaudeCodeCredentialsReader(clock), clock, new ClaudeOAuthUsageOptions());

        var alertPolicy = new UsageAlertPolicy();
        var percentFormatter = new UsagePercentLabelFormatter();

        var fiveHour = new UsageBarViewModel(
            new CountdownResetLabelFormatter(),
            percentFormatter,
            alertPolicy);

        var sevenDay = new UsageBarViewModel(
            new WeekdayResetLabelFormatter(CultureInfo.InvariantCulture, TimeZoneInfo.Local),
            percentFormatter,
            alertPolicy);

        var viewModel = new WidgetViewModel(fiveHour, sevenDay);

        _presenter = new UsageWidgetPresenter(
            viewModel, _provider, clock, FetchInterval, CountdownInterval, dispatcher);

        var window = new MainWindow(
            viewModel, new JsonWidgetPlacementStore(), new Win32DisplayLayoutProvider());
        _trayIcon = new WidgetTrayIcon(window, viewModel);

        return window;
    }

    public void StartRefreshing() => _presenter?.Start();

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _presenter?.Dispose();
        _provider?.Dispose();
    }
}
