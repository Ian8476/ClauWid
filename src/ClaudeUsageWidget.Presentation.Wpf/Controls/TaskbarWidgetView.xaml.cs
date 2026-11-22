using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Controls;

/// <summary>
/// What the widget looks like: the two usage bars and the refresh button, drawn at the size
/// the taskbar slot needs. It is only a view. <see cref="TaskbarChildWindow"/> gives it a
/// native window inside the taskbar, decides where it goes and shows the menu it asks for.
/// </summary>
public partial class TaskbarWidgetView : UserControl
{
    private static readonly string AssemblyName = typeof(TaskbarWidgetView).Assembly.GetName().Name!;
    private static readonly Uri DarkThemeUri = ThemeUri("TaskbarDark");
    private static readonly Uri LightThemeUri = ThemeUri("TaskbarLight");

    private readonly WidgetViewModel _viewModel;

    private ResourceDictionary? _theme;

    public TaskbarWidgetView(WidgetViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        NaturalSize = new Size(Slot.Width, Slot.Height);
        DataContext = viewModel;

        ApplyTheme();
    }

    /// <summary>
    /// The user right-clicked the widget. The menu is not part of this view: a WPF menu cannot
    /// stay open inside the taskbar, so whoever owns the native window shows a native one.
    /// </summary>
    public event EventHandler? MenuRequested;

    /// <summary>Size of the content at zoom 1, in device-independent units.</summary>
    public Size NaturalSize { get; }

    /// <summary>The zoom that makes the content fill a taskbar thicker or thinner than the one it was designed for.</summary>
    public void SetContentScale(double scale)
    {
        ContentScale.ScaleX = scale;
        ContentScale.ScaleY = scale;
    }

    /// <summary>Labels, track and button states for the taskbar's current mode. A no-op when it has not changed.</summary>
    public void ApplyTheme()
    {
        var source = SystemTaskbarTheme.IsLight() ? LightThemeUri : DarkThemeUri;
        if (_theme?.Source == source)
        {
            return;
        }

        var theme = new ResourceDictionary { Source = source };
        var dictionaries = Resources.MergedDictionaries;

        if (_theme is not null)
        {
            dictionaries.Remove(_theme);
        }

        dictionaries.Add(theme);
        _theme = theme;
    }

    private void OnRefreshRequested(object sender, RoutedEventArgs args) => _viewModel.RequestRefresh();

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs args)
    {
        args.Handled = true;
        MenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private static Uri ThemeUri(string name) =>
        new($"/{AssemblyName};component/Theme/{name}.xaml", UriKind.Relative);
}
