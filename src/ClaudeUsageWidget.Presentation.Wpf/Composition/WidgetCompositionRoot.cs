using System.Globalization;
using System.Windows.Threading;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Application.Formatting;
using ClaudeUsageWidget.Domain;
using ClaudeUsageWidget.Infrastructure.Fake;
using ClaudeUsageWidget.Infrastructure.Time;
using ClaudeUsageWidget.Presentation.Wpf.Persistence;
using ClaudeUsageWidget.Presentation.Wpf.Presenters;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using ClaudeUsageWidget.Presentation.Wpf.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Composition;

/// <summary>
/// Unico punto donde se decide que implementacion concreta usa cada puerto.
/// Cambiar el simulador por una fuente real es cambiar una linea de este archivo.
/// No hay contenedor de inyeccion porque a esta escala no aporta nada: todo lo demas
/// ya recibe sus dependencias por constructor, asi que introducirlo despues es aditivo.
/// </summary>
public sealed class WidgetCompositionRoot : IDisposable
{
    private static readonly TimeSpan FetchInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CountdownInterval = TimeSpan.FromSeconds(30);

    private UsageWidgetPresenter? _presenter;

    public MainWindow CreateMainWindow(Dispatcher dispatcher)
    {
        IClock clock = new SystemClock();

        IUsageSnapshotProvider provider = new SimulatedUsageProvider(clock, new SimulatedUsageOptions());

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
            viewModel, provider, clock, FetchInterval, CountdownInterval, dispatcher);

        return new MainWindow(viewModel, new JsonWidgetPlacementStore());
    }

    public void StartRefreshing() => _presenter?.Start();

    public void Dispose() => _presenter?.Dispose();
}
