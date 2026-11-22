namespace ClaudeUsageWidget.Presentation.Wpf.ViewModels;

/// <summary>What the refresh button is doing for the user right now.</summary>
public enum RefreshState
{
    /// <summary>Nothing was asked for. Periodic reads happen in this state too and show nothing.</summary>
    Idle,

    /// <summary>
    /// A refresh was asked for too soon after the last read. The source allows about one a
    /// minute, so it runs as soon as it is allowed.
    /// </summary>
    Waiting,

    /// <summary>The read the user asked for is in flight.</summary>
    Refreshing
}
