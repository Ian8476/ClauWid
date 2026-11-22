namespace ClaudeUsageWidget.Presentation.Wpf.Presenters;

/// <summary>When the presenter reads the source and how it shows that to the user.</summary>
public sealed record UsagePollingOptions
{
    /// <summary>Time between one read and the next automatic one.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The least time between the start of one read and the start of the next, whoever asks for
    /// it: the source rejects reads that come less than about a minute and a half apart.
    /// </summary>
    public TimeSpan MinimumReadSpacing { get; init; } = TimeSpan.FromSeconds(105);

    /// <summary>How often the countdowns on screen are redrawn. Redrawing them costs nothing; reading the source does.</summary>
    public TimeSpan CountdownInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a refresh the user asked for shows as in progress at least. One full turn of
    /// the refresh icon: a read that comes back faster would otherwise leave no trace on screen,
    /// and the click would look ignored.
    /// </summary>
    public TimeSpan MinimumRefreshFeedback { get; init; } = TimeSpan.FromMilliseconds(600);
}
