using System.Windows;
using System.Windows.Input;
using ClaudeUsageWidget.Presentation.Wpf.Persistence;
using ClaudeUsageWidget.Presentation.Wpf.ViewModels;

namespace ClaudeUsageWidget.Presentation.Wpf.Windows;

public partial class MainWindow : Window
{
    private readonly IWidgetPlacementStore _placementStore;

    public MainWindow(WidgetViewModel viewModel, IWidgetPlacementStore placementStore)
    {
        InitializeComponent();

        _placementStore = placementStore;
        DataContext = viewModel;

        RestorePlacement();
    }

    /// <summary>
    /// Sin barra de titulo, la superficie completa es el asa de arrastre.
    /// DragMove lanza InvalidOperationException si el boton ya se solto, de ahi la guarda.
    /// </summary>
    private void OnSurfaceDragStarted(object sender, MouseButtonEventArgs args)
    {
        if (args.ButtonState is not MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        _placementStore.Save(new WidgetPlacement(Left, Top));
    }

    private void OnCloseRequested(object sender, RoutedEventArgs args) => Close();

    private void RestorePlacement()
    {
        if (_placementStore.Load() is not { } placement)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
    }
}
