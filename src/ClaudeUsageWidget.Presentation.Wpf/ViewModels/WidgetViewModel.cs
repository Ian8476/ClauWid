using System.Globalization;
using ClaudeUsageWidget.Domain;

namespace ClaudeUsageWidget.Presentation.Wpf.ViewModels;

/// <summary>
/// Data root of the window. It hands the snapshot to the two bars, says whether the data on
/// screen is still recent and when it was read, and carries the view's refresh requests to
/// whoever reads the source.
/// </summary>
public sealed class WidgetViewModel : ObservableObject
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private UsageSnapshot? _lastSnapshot;
    private bool _isStale;
    private RefreshState _refreshState;
    private DateTimeOffset? _waitingUntil;
    private DateTimeOffset? _lastUpdatedAt;

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

    /// <summary>What the refresh button is doing. Periodic reads do not change it.</summary>
    public RefreshState RefreshState
    {
        get => _refreshState;
        set
        {
            if (SetField(ref _refreshState, value))
            {
                OnPropertyChanged(nameof(RefreshToolTip));
            }
        }
    }

    /// <summary>When the read a waiting refresh is queued behind will run. Only meaningful while <see cref="RefreshState.Waiting"/>.</summary>
    public DateTimeOffset? WaitingUntil
    {
        get => _waitingUntil;
        set
        {
            if (SetField(ref _waitingUntil, value))
            {
                OnPropertyChanged(nameof(RefreshToolTip));
            }
        }
    }

    /// <summary>When the data on screen was read, by the source's clock. Null until the first reading arrives.</summary>
    public DateTimeOffset? LastUpdatedAt
    {
        get => _lastUpdatedAt;
        private set
        {
            if (SetField(ref _lastUpdatedAt, value))
            {
                OnPropertyChanged(nameof(UpdatedLabel));
            }
        }
    }

    /// <summary>What hovering the refresh button says: what a click does now, or why it is queued.</summary>
    public string RefreshToolTip => RefreshState switch
    {
        RefreshState.Waiting => string.Format(
            CultureInfo.InvariantCulture,
            WidgetText.RefreshWaitingToolTipFormat,
            FormatLocalTime(_waitingUntil, WidgetText.TimeWithSecondsFormat)),
        RefreshState.Refreshing => WidgetText.RefreshingToolTip,
        _ => WidgetText.RefreshIdleToolTip
    };

    /// <summary>When the numbers were last read, as the user's clock shows it.</summary>
    public string UpdatedLabel => _lastUpdatedAt is { } updatedAt
        ? string.Format(
            CultureInfo.InvariantCulture,
            WidgetText.UpdatedAtFormat,
            FormatLocalTime(updatedAt, WidgetText.TimeFormat))
        : WidgetText.NoDataYet;

    /// <summary>The user wants a fresh reading now instead of waiting for the next periodic one.</summary>
    public event EventHandler? RefreshRequested;

    public void RequestRefresh() => RefreshRequested?.Invoke(this, EventArgs.Empty);

    public void Apply(UsageSnapshot? snapshot, DateTimeOffset now)
    {
        if (snapshot is not null)
        {
            _lastSnapshot = snapshot;
            LastUpdatedAt = snapshot.CapturedAt;
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

    private static string FormatLocalTime(DateTimeOffset? time, string format) =>
        time?.ToLocalTime().ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
}
