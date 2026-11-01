using System;
using System.IO;
using System.Windows;

namespace DeskSpaceNativeCloseProbe;

// Test-only WPF executable; never included in DeskSpace installation.
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        var root = Path.GetFullPath(args[0]);
        var ready = Path.Combine(root, "ready.txt");
        var attempt = Path.Combine(root, "closing.txt");
        var allow = Path.Combine(root, "allow-close.txt");
        var closed = Path.Combine(root, "closed.txt");
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var window = new Window
            {
                Title = "R232 test-only WPF close veto",
                Width = 230, Height = 140,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow
            };
            window.Closing += (_, e) =>
            {
                File.AppendAllText(attempt, "attempt;");
                if (!File.Exists(allow)) e.Cancel = true;
            };
            window.Closed += (_, _) => File.WriteAllText(closed, "closed");
            window.Loaded += (_, _) => File.WriteAllText(ready, "ready");
            app.Run(window);
            return File.Exists(allow) && File.Exists(closed) ? 0 : 3;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(root, "error.txt"), ex.ToString());
            return 4;
        }
    }
}
