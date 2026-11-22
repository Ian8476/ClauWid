using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Win32;
using ClaudeUsageWidget.Application.Abstractions;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Presenters;

/// <summary>
/// Mantiene el ViewModel sincronizado con el paso del tiempo y con la fuente de datos.
/// Son dos cadencias distintas a proposito: consultar la fuente es caro, redibujar la
/// cuenta regresiva no lo es.
/// <para>
/// The source allows a read only every minute and a half or so, so reads are not on a fixed clock: a single
/// one-shot timer always points at the next read, and every decision about it comes from two
/// facts. When the last read started, which with the minimum spacing gives the earliest the
/// next one may start, and the time the source itself asked to wait until, if it did.
/// Whoever wants a read earlier than that, the user's refresh button included, is queued
/// for exactly that moment instead of being sent to be rejected.
/// </para>
/// </summary>
public sealed class UsageWidgetPresenter : IDisposable
{
    private readonly WidgetViewModel _viewModel;
    private readonly IUsageSnapshotProvider _provider;
    private readonly IClock _clock;
    private readonly UsagePollingOptions _options;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _nextReadTimer;
    private readonly DispatcherTimer _countdownTimer;
    private readonly CancellationTokenSource _lifetime = new();

    private DateTimeOffset? _lastAttemptAt;
    private DateTimeOffset? _notBefore;
    private DateTimeOffset? _scheduledAt;
    private DateTimeOffset _refreshingSince;
    private bool _isFetching;
    private bool _isPaused = true;

    public UsageWidgetPresenter(
        WidgetViewModel viewModel,
        IUsageSnapshotProvider provider,
        IClock clock,
        UsagePollingOptions options,
        Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _provider = provider;
        _clock = clock;
        _options = options;
        _dispatcher = dispatcher;

        _nextReadTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _nextReadTimer.Tick += OnNextReadDue;

        _countdownTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = options.CountdownInterval
        };
        _countdownTimer.Tick += (_, _) => _viewModel.Refresh(_clock.Now);

        _viewModel.RefreshRequested += OnRefreshRequested;
    }

    /// <summary>
    /// The earliest moment the next read may start: the minimum spacing after the last one,
    /// or the time the source asked to wait until, whichever is later.
    /// </summary>
    private DateTimeOffset EarliestRead
    {
        get
        {
            var afterSpacing = _lastAttemptAt is { } last
                ? last + _options.MinimumReadSpacing
                : DateTimeOffset.MinValue;

            return _notBefore is { } notBefore && notBefore > afterSpacing ? notBefore : afterSpacing;
        }
    }

    /// <summary>
    /// Arranca los timers y lanza la primera lectura sin bloquear el arranque de la UI.
    /// La ventana se dibuja de inmediato con las etiquetas vacias y se rellena al primer dato.
    /// </summary>
    public void Start()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        Resume();
    }

    /// <summary>
    /// Nobody can see the widget on a locked session, so polling stops until the unlock,
    /// which reads right away instead of waiting out the interval, unless that would be too
    /// soon after the last read.
    /// SystemEvents may raise this off the UI thread, and the timers belong to the dispatcher.
    /// </summary>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
    {
        switch (args.Reason)
        {
            case SessionSwitchReason.SessionLock:
                _dispatcher.InvokeAsync(Pause);
                break;

            case SessionSwitchReason.SessionUnlock:
                _dispatcher.InvokeAsync(Resume);
                break;
        }
    }

    private void Pause()
    {
        _isPaused = true;
        _nextReadTimer.Stop();
        _scheduledAt = null;
        _countdownTimer.Stop();
    }

    private void Resume()
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }

        _isPaused = false;
        _viewModel.Refresh(_clock.Now);
        _countdownTimer.Start();
        ReadWhenAllowed();
    }

    /// <summary>Reads now if the spacing allows it, and otherwise at the first moment it does.</summary>
    private void ReadWhenAllowed()
    {
        if (_clock.Now >= EarliestRead)
        {
            _ = ReadAsync();
            return;
        }

        Schedule(EarliestRead);
    }

    /// <summary>
    /// The refresh button. A read in flight is the fresh data the user asked for, so none is
    /// added and the button just follows it. Otherwise the read starts now if the source
    /// would accept it, and if not the click is queued for the first moment it would: the
    /// button shows that it is waiting, and the read runs then, once, however many clicks come.
    /// </summary>
    private async void OnRefreshRequested(object? sender, EventArgs args)
    {
        if (_isPaused || _lifetime.IsCancellationRequested || _viewModel.RefreshState is not RefreshState.Idle)
        {
            return;
        }

        var now = _clock.Now;

        if (_isFetching)
        {
            BeginRefreshing(now);
            return;
        }

        var earliest = EarliestRead;
        if (now >= earliest)
        {
            BeginRefreshing(now);
            await ReadAsync();
            return;
        }

        BeginWaiting(earliest);
        if (_scheduledAt is not { } scheduled || earliest < scheduled)
        {
            Schedule(earliest);
        }
    }

    private async void OnNextReadDue(object? sender, EventArgs args)
    {
        _nextReadTimer.Stop();
        _scheduledAt = null;

        // The timer and the clock are not the same source of time and may disagree by a few
        // milliseconds. Asking a rate-limited source early would only cost a rejection.
        if (_clock.Now < EarliestRead)
        {
            Schedule(EarliestRead);
            return;
        }

        await ReadAsync();
    }

    /// <summary>
    /// Una lectura lenta no debe encolar otra encima. Si la anterior sigue viva,
    /// este tick se descarta en lugar de acumular trabajo. When it ends, the next read is
    /// scheduled from what it said: a rate limit means the time the source gave, anything
    /// else means a whole poll interval from now.
    /// </summary>
    private async Task ReadAsync()
    {
        if (_isFetching || _lifetime.IsCancellationRequested)
        {
            return;
        }

        _isFetching = true;
        _lastAttemptAt = _clock.Now;
        _nextReadTimer.Stop();
        _scheduledAt = null;

        if (_viewModel.RefreshState is RefreshState.Waiting)
        {
            BeginRefreshing(_lastAttemptAt.Value);
        }

        var result = UsageReadResult.NoData;
        try
        {
            result = await _provider.GetLatestAsync(_lifetime.Token);
            _viewModel.Apply(result.Snapshot, _clock.Now);
        }
        catch (OperationCanceledException)
        {
            // Cierre de la aplicacion. No hay nada que reportar.
            return;
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

        _notBefore = result.NotBefore;
        var nextRead = ScheduleNextRead(result);

        await EndRefreshingAsync(result.Snapshot is null && result.NotBefore is not null ? nextRead : null);
    }

    private DateTimeOffset ScheduleNextRead(UsageReadResult result)
    {
        var earliest = EarliestRead;
        var next = result.NotBefore is not null
            ? earliest
            : Max(_clock.Now + _options.PollInterval, earliest);

        Schedule(next);
        return next;
    }

    /// <summary>
    /// Points the one-shot timer at the given moment, replacing wherever it pointed before.
    /// Does nothing while paused: the unlock picks the schedule up again.
    /// </summary>
    private void Schedule(DateTimeOffset at)
    {
        if (_isPaused || _lifetime.IsCancellationRequested)
        {
            return;
        }

        var delay = at - _clock.Now;

        _nextReadTimer.Stop();
        _nextReadTimer.Interval = delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        _scheduledAt = at;
        _nextReadTimer.Start();
    }

    private void BeginRefreshing(DateTimeOffset now)
    {
        _refreshingSince = now;
        _viewModel.RefreshState = RefreshState.Refreshing;
        _viewModel.WaitingUntil = null;
    }

    private void BeginWaiting(DateTimeOffset until)
    {
        _viewModel.WaitingUntil = until;
        _viewModel.RefreshState = RefreshState.Waiting;
    }

    /// <summary>
    /// Ends the user's refresh once it has shown for the minimum time. If the source rejected
    /// the read and asked to wait, the refresh is not over: it goes back to waiting for the
    /// retry that is already scheduled, instead of looking done with nothing new on screen.
    /// </summary>
    private async Task EndRefreshingAsync(DateTimeOffset? retryAt)
    {
        if (_viewModel.RefreshState is not RefreshState.Refreshing)
        {
            return;
        }

        var remaining = _options.MinimumRefreshFeedback - (_clock.Now - _refreshingSince);
        if (remaining > TimeSpan.Zero && !_lifetime.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(remaining, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The app is closing: the state below is the last thing to settle.
            }
        }

        if (retryAt is { } retry && !_lifetime.IsCancellationRequested)
        {
            BeginWaiting(retry);
            return;
        }

        _viewModel.RefreshState = RefreshState.Idle;
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;

    public void Dispose()
    {
        // A static event: left subscribed, it would keep this presenter alive.
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _viewModel.RefreshRequested -= OnRefreshRequested;
        Pause();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
