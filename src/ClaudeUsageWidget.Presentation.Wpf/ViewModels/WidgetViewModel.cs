using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Presentation.Wpf.ViewModels;

/// <summary>
/// Data root of the window. It hands the snapshot to the two bars, says whether the data on
/// screen is still recent, and carries the view's refresh requests to whoever reads the source.
/// </summary>
public sealed class WidgetViewModel : ObservableObject
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private UsageSnapshot? _lastSnapshot;
    private bool _isStale;
    private bool _isRefreshing;

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

    /// <summary>True while a refresh the user asked for is in flight. The periodic reads do not set it.</summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetField(ref _isRefreshing, value);
    }

    /// <summary>The user wants a fresh reading now instead of waiting for the next periodic one.</summary>
    public event EventHandler? RefreshRequested;

    public void RequestRefresh() => RefreshRequested?.Invoke(this, EventArgs.Empty);

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
