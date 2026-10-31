using System.Diagnostics;
using System.Windows;

namespace DeskSpace;
public partial class App : Application
{
    private Mutex? widgetMutex;
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
            var window=new WidgetShellWindow();MainWindow=window;window.Show();return;
        }
        ShutdownMode=ShutdownMode.OnMainWindowClose;
        var main=new MainWindow();MainWindow=main;main.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        try {widgetMutex?.ReleaseMutex();}catch(ApplicationException){}
        widgetMutex?.Dispose();base.OnExit(e);
    }
}
