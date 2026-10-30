using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DeskSpace;

// R200: user-initiated feature navigation across the existing DeskSpace
// widget process and main Workspace. Same-user named pipe, bounded wait,
// fixed protocol verbs; never execute a path or command received via IPC.
public partial class MainWindow
{
    internal void ShowFeatureDestinationV200(string destination)
    {
        if(!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(()=>
                ShowFeatureDestinationV200(destination)));
            return;
        }
        if(!IsVisible)Show();
        if(WindowState==WindowState.Minimized)
            WindowState=WindowState.Normal;
        if(destination=="FEATURES")
        {
            var tab=MainTabs.Items.OfType<TabItem>().FirstOrDefault(
                item=>string.Equals(item.Tag?.ToString(),"DeskSpaceFeatures",
                    StringComparison.Ordinal));
            if(tab!=null)MainTabs.SelectedItem=tab;
        }
        else if(destination is "NOTES" or "REMINDERS")
            SelectExistingFeatureTabV199(destination=="NOTES"?"Notes":"Reminders");
        Activate();
    }
}

internal static class DeskSpaceFeatureNavigationV200
{
    private const string Pipe="DeskSpace.Workspace.ExcelMapping.V49";
    private static readonly string[] Allowed={"FEATURES","NOTES","REMINDERS"};
    internal static bool IsAllowed(string? destination)=>destination!=null &&
        Allowed.Contains(destination,StringComparer.Ordinal);
    private static string Argument(string destination)=>destination switch
    {
        // Reuse the pre-existing switch that launches Workspace rather than
        // another widget host; the second fixed switch chooses the destination.
        "FEATURES"=>"--excel-mapping --features",
        "NOTES"=>"--excel-mapping --notes",
        "REMINDERS"=>"--excel-mapping --reminders",
        _=>throw new ArgumentOutOfRangeException(nameof(destination))
    };

    internal static async Task OpenAsync(Window owner,string destination)
    {
        if(!IsAllowed(destination))
            throw new ArgumentException("Unsupported DeskSpace destination.");
        try
        {
            if(Application.Current!=null)
            {
                foreach(Window window in Application.Current.Windows)
                {
                    if(window is MainWindow workspace)
                    {
                        workspace.ShowFeatureDestinationV200(destination);
                        return;
                    }
                }
            }
            // CurrentUserOnly was introduced by the existing Excel navigation
            // bridge. Reuse that server instead of creating a second listener.
            for(int attempt=0;attempt<3;attempt++)
            {
                try
                {
                    using var client=new NamedPipeClientStream(".",Pipe,
                        PipeDirection.Out,PipeOptions.Asynchronous|
                        PipeOptions.CurrentUserOnly);
                    using var timeout=new CancellationTokenSource(
                        TimeSpan.FromMilliseconds(450));
                    await client.ConnectAsync(timeout.Token);
                    using var writer=new StreamWriter(client){AutoFlush=true};
                    await writer.WriteLineAsync(destination);
                    return;
                }
                catch(TimeoutException){}
                catch(OperationCanceledException){}
                catch(IOException){}
                catch(UnauthorizedAccessException){break;}
                if(attempt<2)await Task.Delay(100);
            }
            // No Workspace process has answered; launch only the *installed*
            // local DeskSpace binary, with one hardcoded, approved argument.
            string exe=Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe");
            if(!File.Exists(exe))
                throw new FileNotFoundException("DeskSpace Workspace is not installed.");
            var process=Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute=true,Arguments=Argument(destination)
            });
            if(process==null)
                throw new IOException("Could not open DeskSpace Workspace.");
        }
        catch(Exception ex)when(ex is IOException or InvalidOperationException or
            UnauthorizedAccessException or System.ComponentModel.Win32Exception or
            System.Security.SecurityException)
        {
            if(owner.IsLoaded)
                owner.Dispatcher.BeginInvoke(new Action(()=>
                    MessageBox.Show(owner,"Unable to open Workspace: "+
                        ex.GetType().Name,"DeskSpace Features",
                        MessageBoxButton.OK,MessageBoxImage.Warning)));
        }
    }

    internal static async Task OpenExcelAsync(Window owner)
    {
        try
        {
            await DeskSpaceExcelManageV49.OpenInsideWorkspaceAsync();
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or
            UnauthorizedAccessException or System.ComponentModel.Win32Exception or
            System.Security.SecurityException)
        {
            if(owner.IsLoaded)
                owner.Dispatcher.BeginInvoke(new Action(()=>
                    MessageBox.Show(owner,"Unable to open Excel mappings: "+
                        ex.GetType().Name,"DeskSpace Features",
                        MessageBoxButton.OK,MessageBoxImage.Warning)));
        }
    }
}
