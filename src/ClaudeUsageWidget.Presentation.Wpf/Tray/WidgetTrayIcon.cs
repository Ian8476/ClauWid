using System.ComponentModel;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ClaudeUsageWidget.Presentation.Wpf.Tray;

/// <summary>
/// Notification area icon: the way to bring the widget back when it is hidden, and to quit
/// without having to see it. WPF has no NotifyIcon of its own, so the WinForms one is borrowed.
/// Windows 11 puts new icons in the hidden icons flyout by default.
/// </summary>
public sealed class WidgetTrayIcon : IDisposable
{
    private const string AppName = "Claude usage";
    private const string FiveHourCaption = "5 h";
    private const string SevenDayCaption = "Semana";
    private const string ShowWidgetText = "Mostrar widget";
    private const string HideWidgetText = "Ocultar widget";
    private const string RefreshText = "Actualizar ahora";
    private const string ExitText = "Salir";

    // Limite de NotifyIcon.Text: un texto mas largo lanza ArgumentOutOfRangeException.
    private const int MaxTooltipLength = 127;

    // Named after this assembly instead of relying on the application's own resources, which
    // belong to whichever exe hosts the code: under a test runner that is the runner, not the
    // widget. Relative on purpose: an absolute pack:// URI only parses once WPF has registered
    // the scheme, and nothing guarantees that has happened by the time this field is set.
    private static readonly Uri IconUri = new(
        $"/{typeof(WidgetTrayIcon).Assembly.GetName().Name};component/Assets/Icons/ClaudeUsageWidget.ico",
        UriKind.Relative);

    private readonly IWidgetHost _host;
    private readonly WidgetViewModel _viewModel;
    private readonly Dispatcher _dispatcher;
    private readonly Drawing.Icon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.NotifyIcon _notifyIcon;

    private DispatcherOperation? _pendingTooltipUpdate;

    public WidgetTrayIcon(IWidgetHost host, WidgetViewModel viewModel, Dispatcher dispatcher)
    {
        _host = host;
        _viewModel = viewModel;
        _dispatcher = dispatcher;

        // El .ico trae varios tamanos; pedir el del sistema evita que Windows reescale el grande.
        using (var stream = System.Windows.Application.GetResourceStream(IconUri).Stream)
        {
            _icon = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }

        _toggleItem = new Forms.ToolStripMenuItem(HideWidgetText, null, (_, _) => ToggleWidget());

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(new Forms.ToolStripMenuItem(RefreshText, null, (_, _) => _viewModel.RequestRefresh()));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(new Forms.ToolStripMenuItem(ExitText, null, (_, _) => _host.Exit()));
        _menu.Opening += OnMenuOpening;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = AppName,
            ContextMenuStrip = _menu,
        };
        _notifyIcon.MouseClick += OnNotifyIconClicked;

        _viewModel.FiveHour.PropertyChanged += OnUsageChanged;
        _viewModel.SevenDay.PropertyChanged += OnUsageChanged;
        UpdateTooltip();

        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        _viewModel.FiveHour.PropertyChanged -= OnUsageChanged;
        _viewModel.SevenDay.PropertyChanged -= OnUsageChanged;
        _pendingTooltipUpdate?.Abort();

        // Sin ocultarlo antes, el icono se queda pintado hasta que el raton pasa por encima.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private void OnNotifyIconClicked(object? sender, Forms.MouseEventArgs args)
    {
        if (args.Button is Forms.MouseButtons.Left)
        {
            ToggleWidget();
        }
    }

    /// <summary>The menu shows the widget's actual state, which its own menu changes too.</summary>
    private void OnMenuOpening(object? sender, CancelEventArgs args) =>
        _toggleItem.Text = _host.IsVisible ? HideWidgetText : ShowWidgetText;

    private void ToggleWidget()
    {
        if (_host.IsVisible)
        {
            _host.Hide();
            return;
        }

        _host.Show();
    }

    /// <summary>
    /// One refresh changes several properties on both bars in a row. Instead of rebuilding
    /// the tooltip, and calling into the shell, once per property, a single update is queued
    /// behind them. Fraction and Severity are not in the text, so they are ignored.
    /// </summary>
    private void OnUsageChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(UsageBarViewModel.PercentLabel)
            or nameof(UsageBarViewModel.ResetLabel)
            or nameof(UsageBarViewModel.HasData)))
        {
            return;
        }

        _pendingTooltipUpdate ??= _dispatcher.InvokeAsync(UpdateTooltip, DispatcherPriority.Background);
    }

    /// <summary>Hovering the icon gives the usage as text, also while the widget is hidden.</summary>
    private void UpdateTooltip()
    {
        _pendingTooltipUpdate = null;

        var text = string.Join(
            Environment.NewLine,
            AppName,
            FormatLine(FiveHourCaption, _viewModel.FiveHour),
            FormatLine(SevenDayCaption, _viewModel.SevenDay));

        _notifyIcon.Text = text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;
    }

    private static string FormatLine(string caption, UsageBarViewModel bar) =>
        bar.HasData
            ? $"{caption}: {bar.PercentLabel} · {bar.ResetLabel}"
            : $"{caption}: {bar.PercentLabel}";
}
