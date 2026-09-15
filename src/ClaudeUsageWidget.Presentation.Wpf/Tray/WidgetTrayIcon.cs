using System.ComponentModel;
using System.Windows;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ClaudeUsageWidget.Presentation.Wpf.Tray;

/// <summary>
/// Icono en el area de notificacion. Como el widget no aparece en la barra de tareas, es
/// la unica forma de encontrarlo cuando esta oculto y de salir sin tener que verlo.
/// WPF no trae NotifyIcon propio, asi que se toma prestado de WinForms.
/// Windows 11 coloca los iconos nuevos en el desplegable de iconos ocultos por defecto.
/// </summary>
public sealed class WidgetTrayIcon : IDisposable
{
    private const string AppName = "Claude usage";
    private const string FiveHourCaption = "5 h";
    private const string SevenDayCaption = "Semana";
    private const string ShowWidgetText = "Mostrar widget";
    private const string HideWidgetText = "Ocultar widget";
    private const string TopmostText = "Siempre visible";
    private const string ExitText = "Salir";

    // Limite de NotifyIcon.Text: un texto mas largo lanza ArgumentOutOfRangeException.
    private const int MaxTooltipLength = 127;

    private static readonly Uri IconUri = new("pack://application:,,,/Assets/Icons/ClaudeUsageWidget.ico");

    private readonly Window _window;
    private readonly WidgetViewModel _viewModel;
    private readonly Drawing.Icon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.ToolStripMenuItem _topmostItem;
    private readonly Forms.NotifyIcon _notifyIcon;

    public WidgetTrayIcon(Window window, WidgetViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;

        // El .ico trae varios tamanos; pedir el del sistema evita que Windows reescale el grande.
        using (var stream = System.Windows.Application.GetResourceStream(IconUri).Stream)
        {
            _icon = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }

        _toggleItem = new Forms.ToolStripMenuItem(HideWidgetText, null, (_, _) => ToggleWindow());
        _topmostItem = new Forms.ToolStripMenuItem(TopmostText, null, (_, _) => _window.Topmost = !_window.Topmost);

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(_topmostItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(new Forms.ToolStripMenuItem(ExitText, null, (_, _) => _window.Close()));
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
            ToggleWindow();
        }
    }

    /// <summary>El menu refleja el estado real de la ventana, que tambien cambia desde su propio menu.</summary>
    private void OnMenuOpening(object? sender, CancelEventArgs args)
    {
        _toggleItem.Text = _window.IsVisible ? HideWidgetText : ShowWidgetText;
        _topmostItem.Checked = _window.Topmost;
    }

    private void ToggleWindow()
    {
        if (_window.IsVisible)
        {
            _window.Hide();
            return;
        }

        _window.Show();
        _window.Activate();
    }

    private void OnUsageChanged(object? sender, PropertyChangedEventArgs args) => UpdateTooltip();

    /// <summary>Pasar el raton por el icono da el consumo sin tener que abrir el widget.</summary>
    private void UpdateTooltip()
    {
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
