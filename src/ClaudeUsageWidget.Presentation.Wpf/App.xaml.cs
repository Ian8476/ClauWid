using System.Windows;
using System.Windows.Threading;
using ClaudeUsageWidget.Presentation.Wpf.Composition;

namespace ClaudeUsageWidget.Presentation.Wpf;

public partial class App : System.Windows.Application
{
    private readonly WidgetCompositionRoot _compositionRoot = new();

    /// <summary>
    /// El arranque es sincrono a proposito. Con un "async void OnStartup" la ventana se
    /// crea despues de que WPF ya siguio adelante, y cualquier excepcion durante la
    /// construccion se pierde sin dejar rastro.
    /// </summary>
    protected override void OnStartup(StartupEventArgs args)
    {
        base.OnStartup(args);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        MainWindow = _compositionRoot.CreateMainWindow(Dispatcher);
        MainWindow.Show();

        _compositionRoot.StartRefreshing();
    }

    protected override void OnExit(ExitEventArgs args)
    {
        _compositionRoot.Dispose();
        base.OnExit(args);
    }

    /// <summary>
    /// Sin ventana principal visible un fallo pasaria desapercibido, asi que se muestra.
    /// El widget se cierra despues: un widget que miente sobre tu consumo es peor que ninguno.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        MessageBox.Show(
            args.Exception.ToString(),
            "Claude usage widget",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        args.Handled = true;
        Shutdown();
    }
}
