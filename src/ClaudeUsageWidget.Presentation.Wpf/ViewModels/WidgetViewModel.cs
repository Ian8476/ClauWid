using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Presentation.Wpf.ViewModels;

/// <summary>
/// Raiz de datos de la ventana. Solo reparte el snapshot entre las dos barras
/// y expone si el dato que se esta mostrando sigue siendo reciente.
/// </summary>
public sealed class WidgetViewModel : ObservableObject
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private UsageSnapshot? _lastSnapshot;
    private bool _isStale;

    public WidgetViewModel(UsageBarViewModel fiveHour, UsageBarViewModel sevenDay)
    {
        FiveHour = fiveHour;
        SevenDay = sevenDay;
    }

    public UsageBarViewModel FiveHour { get; }

    public UsageBarViewModel SevenDay { get; }

    public bool IsStale
    {
        get => _isStale;
        private set => SetField(ref _isStale, value);
    }

    public void Apply(UsageSnapshot? snapshot, DateTimeOffset now)
    {
        if (snapshot is not null)
        {
            _lastSnapshot = snapshot;
        }

        Refresh(now);
    }

    /// <summary>
    /// Vuelve a proyectar el ultimo snapshot conocido. Se llama cada minuto para que
    /// la cuenta regresiva avance aunque no haya llegado un dato nuevo.
    /// </summary>
    public void Refresh(DateTimeOffset now)
    {
        FiveHour.Apply(_lastSnapshot?.FiveHour, now);
        SevenDay.Apply(_lastSnapshot?.SevenDay, now);
        IsStale = _lastSnapshot is null || _lastSnapshot.AgeAt(now) > StaleAfter;
    }
}
