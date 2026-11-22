using System.Globalization;
using System.Windows.Threading;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;
using ClaudeUsageWidget.Infrastructure.ClaudeCode;
using ClaudeUsageWidget.Infrastructure.Time;
using ClaudeUsageWidget.Presentation.Wpf.Presenters;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Presentation.Wpf.Tray;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Composition;

/// <summary>
/// Unico punto donde se decide que implementacion concreta usa cada puerto.
/// Volver al simulador (Infrastructure.Fake) para iterar la UI es cambiar la linea del proveedor.
/// No hay contenedor de inyeccion porque a esta escala no aporta nada: todo lo demas
/// ya recibe sus dependencias por constructor, asi que introducirlo despues es aditivo.
/// </summary>
public sealed class WidgetCompositionRoot : IDisposable
{
    // Each read is an authenticated network call, and the API rejects reads less than about
    // 105 s apart (measured 2026-11-22). Polling every three minutes stays inside that and
    // leaves a window in which the refresh button reads right away; earlier clicks are held
    // back to the spacing instead of being sent to be rejected. Usage in a 5-hour window moves
    // slowly, so three minutes is still fresh.
    private static readonly UsagePollingOptions PollingOptions = new()
    {
        PollInterval = TimeSpan.FromMinutes(3),
        MinimumReadSpacing = TimeSpan.FromSeconds(105)
    };

    private ClaudeOAuthUsageProvider? _provider;
    private UsageWidgetPresenter? _presenter;
    private TaskbarWidgetHost? _host;
    private WidgetTrayIcon? _trayIcon;

    /// <summary>
    /// Builds the widget, docks it to the taskbar and starts reading. <paramref name="exit"/>
    /// is how the widget asks the app to shut down: no window's lifetime decides that.
    /// </summary>
    public void Start(Dispatcher dispatcher, Action exit)
    {
        IClock clock = new SystemClock();

        _provider = new ClaudeOAuthUsageProvider(
            new ClaudeCodeCredentialsReader(clock),
            new ClaudeCliSessionRefresher(clock, new ClaudeCliSessionRefresherOptions()),
            clock,
            new ClaudeOAuthUsageOptions());

        var severityPolicy = new UsageSeverityPolicy();
        var percentFormatter = new UsagePercentLabelFormatter();

        var fiveHour = new UsageBarViewModel(
            new CountdownResetLabelFormatter(),
            percentFormatter,
            severityPolicy);

        var sevenDay = new UsageBarViewModel(
            new WeekdayResetLabelFormatter(CultureInfo.InvariantCulture, TimeZoneInfo.Local),
            percentFormatter,
            severityPolicy);

        var viewModel = new WidgetViewModel(fiveHour, sevenDay);

        _presenter = new UsageWidgetPresenter(
            viewModel, _provider, clock, PollingOptions, dispatcher);

        _host = new TaskbarWidgetHost(viewModel, new Win32TaskbarLocator(), dispatcher, exit);
        _trayIcon = new WidgetTrayIcon(_host, viewModel, dispatcher);

        _host.Start();
        _presenter.Start();
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _host?.Dispose();
        _presenter?.Dispose();
        _provider?.Dispose();
    }
}
