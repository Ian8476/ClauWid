using System.Diagnostics;
using System.Windows.Threading;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Presenters;

/// <summary>
/// Mantiene el ViewModel sincronizado con el paso del tiempo y con la fuente de datos.
/// Son dos cadencias distintas a proposito: consultar la fuente es caro, redibujar la
/// cuenta regresiva no lo es.
/// </summary>
public sealed class UsageWidgetPresenter : IDisposable
{
    private readonly WidgetViewModel _viewModel;
    private readonly IUsageSnapshotProvider _provider;
    private readonly IClock _clock;
    private readonly DispatcherTimer _fetchTimer;
    private readonly DispatcherTimer _countdownTimer;
    private readonly CancellationTokenSource _lifetime = new();

    private bool _isFetching;

    public UsageWidgetPresenter(
        WidgetViewModel viewModel,
        IUsageSnapshotProvider provider,
        IClock clock,
        TimeSpan fetchInterval,
        TimeSpan countdownInterval,
        Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _provider = provider;
        _clock = clock;

        _fetchTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = fetchInterval
        };
        _fetchTimer.Tick += async (_, _) => await FetchAsync();

        _countdownTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = countdownInterval
        };
        _countdownTimer.Tick += (_, _) => _viewModel.Refresh(_clock.Now);
    }

    /// <summary>
    /// Arranca los timers y lanza la primera lectura sin bloquear el arranque de la UI.
    /// La ventana se dibuja de inmediato con las etiquetas vacias y se rellena al primer dato.
    /// </summary>
    public void Start()
    {
        _viewModel.Refresh(_clock.Now);
        _fetchTimer.Start();
        _countdownTimer.Start();
        _ = FetchAsync();
    }

    /// <summary>
    /// Una lectura lenta no debe encolar otra encima. Si la anterior sigue viva,
    /// este tick se descarta en lugar de acumular trabajo.
    /// </summary>
    private async Task FetchAsync()
    {
        if (_isFetching || _lifetime.IsCancellationRequested)
        {
            return;
        }

        _isFetching = true;
        try
        {
            var snapshot = await _provider.GetLatestAsync(_lifetime.Token);
            _viewModel.Apply(snapshot, _clock.Now);
        }
        catch (OperationCanceledException)
        {
            // Cierre de la aplicacion. No hay nada que reportar.
        }
        catch (Exception exception)
        {
            // Una fuente caida no puede tumbar el widget: se conserva el ultimo dato
            // y WidgetViewModel lo marcara como obsoleto por su edad. El motivo queda
            // en la ventana Salida de Visual Studio al depurar.
            Debug.WriteLine($"Claude usage: fallo la lectura. {exception}");
        }
        finally
        {
            _isFetching = false;
        }
    }

    public void Dispose()
    {
        _fetchTimer.Stop();
        _countdownTimer.Stop();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
