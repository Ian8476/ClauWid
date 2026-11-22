namespace ClaudeUsageWidget.Presentation.Wpf;

/// <summary>The texts of the widget's own UI, in the language the rest of it uses.</summary>
public static class WidgetText
{
    public const string RefreshIdleToolTip = "Actualizar ahora";
    public const string RefreshingToolTip = "Actualizando…";

    /// <summary>{0}: the time the read will run, with seconds, as the user's clock shows it.</summary>
    public const string RefreshWaitingToolTipFormat =
        "En espera: la API limita las lecturas seguidas. Se actualizará a las {0}";

    /// <summary>{0}: the time of the last reading, without seconds.</summary>
    public const string UpdatedAtFormat = "Actualizado a las {0}";

    public const string NoDataYet = "Sin datos todavía";

    public const string TimeWithSecondsFormat = "HH:mm:ss";
    public const string TimeFormat = "HH:mm";
}
