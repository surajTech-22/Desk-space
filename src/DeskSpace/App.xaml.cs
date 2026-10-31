using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DeskSpace;
public partial class App : Application
{
    private Mutex? widgetMutex;
    private bool widgetStarted;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Contains("--widget-host",StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode=ShutdownMode.OnMainWindowClose;
            widgetMutex=new Mutex(true,@"Local\DeskSpace.WidgetHost.v3",out var first);
            if(!first)
            {
                try { using var client = new System.IO.Pipes.NamedPipeClientStream(".", "DeskSpace.Widget.V3", System.IO.Pipes.PipeDirection.Out); client.Connect(400); client.WriteByte(1); client.Flush(); } catch { }
                Shutdown(); return;
            }
            // Persist the short-start marker before widget/tray creation.
            // Failure in the constructor now contributes to safe recovery.
            DeskSpaceStartupRecoveryV183.BeginEarly();
            try
            {
                var window=new WidgetShellWindow();
                MainWindow=window;
                window.Show();
                widgetStarted=true;
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception or
                System.Runtime.InteropServices.COMException or
                System.Windows.Markup.XamlParseException or
                TypeInitializationException)
            {
                Debug.WriteLine("DeskSpace widget early startup failed: "+
                    ex.GetType().Name);
                // Keep a usable, single-instance recovery window and tray
                // without deleting settings or entering a relaunch storm.
                DeskSpaceStartupFallbackV276.Show(ex.GetType().Name);
            }
            return;
        }
        ShutdownMode=ShutdownMode.OnMainWindowClose;
        var main=new MainWindow();MainWindow=main;main.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        // Only a successfully shown widget can mark a normal process exit.
        // A failed constructor must remain recorded as an unhealthy start.
        if(widgetStarted)DeskSpaceStartupRecoveryV183.MarkCleanExit();
        try {widgetMutex?.ReleaseMutex();}catch(ApplicationException){}
        widgetMutex?.Dispose();base.OnExit(e);
    }
}
