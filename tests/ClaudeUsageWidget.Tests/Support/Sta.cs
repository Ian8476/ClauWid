using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClaudeUsageWidget.Tests.Support;

/// <summary>
/// Runs a test on its own STA thread with a WPF dispatcher, the way the widget's UI thread
/// runs, and pumps that dispatcher on demand so timers and queued work actually happen.
/// </summary>
internal static class Sta
{
    static Sta()
    {
        // SystemEvents creates its hidden window on the first thread that needs it. Created on a
        // short-lived test thread, the window would die with it; created from an MTA thread, it
        // gets a dedicated thread that lives as long as the process.
        var warmUp = new Thread(static () => SystemEvents.InvokeOnEventsThread(static () => { }));
        warmUp.SetApartmentState(ApartmentState.MTA);
        warmUp.Start();
        warmUp.Join();
    }

    public static void Run(Action test)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            try
            {
                test();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                dispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }

    /// <summary>Processes the current dispatcher's work for the given time.</summary>
    public static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer(DispatcherPriority.Send) { Interval = duration };
        stop.Tick += (_, _) =>
        {
            stop.Stop();
            frame.Continue = false;
        };

        stop.Start();
        Dispatcher.PushFrame(frame);
    }
}
