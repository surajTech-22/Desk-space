using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DeskSpace;

// Navigate to the EXISTING Excel Mapping page, never launch the Excel
// mapping editor in another popup. The widget may run in a separate process.
public partial class MainWindow
{
    private CancellationTokenSource? excelNavigationCancel;
    private bool excelNavigationStarted;
    private const string ExcelNavigationPipe="DeskSpace.Workspace.ExcelMapping.V49";

    private void InitializeExcelNavigationV49()
    {
        if(excelNavigationStarted)return;
        excelNavigationStarted=true;
        Loaded+=(_,_)=>{
            _=RunExcelNavigationPipeV49();
            if(Environment.GetCommandLineArgs().Any(x=>
                x.Equals("--excel-mapping",StringComparison.OrdinalIgnoreCase)))
                ShowExcelMappingFromWheelV49();
        };
        Closed+=(_,_)=>excelNavigationCancel?.Cancel();
    }
    public void ShowExcelMappingFromWheelV49()
    {
        if(!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(ShowExcelMappingFromWheelV49));
            return;
        }
        if(!IsVisible)Show();
        if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
        MainTabs.SelectedItem=ExcelMainTab;
        Activate();
        Focus();
    }
    private async Task RunExcelNavigationPipeV49()
    {
        excelNavigationCancel=new CancellationTokenSource();
        var cancellation=excelNavigationCancel.Token;
        while(!cancellation.IsCancellationRequested)
        {
            try
            {
                using var server=new NamedPipeServerStream(
                    ExcelNavigationPipe,PipeDirection.In,1,
                    PipeTransmissionMode.Byte,PipeOptions.Asynchronous |
                    PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellation);
                using var input=new StreamReader(server);
                var command=await input.ReadLineAsync(cancellation);
                if(string.Equals(command,"EXCEL_MAPPING",StringComparison.Ordinal))
                    await Dispatcher.InvokeAsync(ShowExcelMappingFromWheelV49);
            }
            catch(OperationCanceledException){break;}
            catch(IOException){await Task.Delay(250,cancellation);}
            catch(UnauthorizedAccessException){break;}
        }
    }
}
internal static class DeskSpaceExcelManageV49
{
    private const string Pipe="DeskSpace.Workspace.ExcelMapping.V49";
    internal static async Task OpenInsideWorkspaceAsync()
    {
        // Same-process uses the actual MainWindow tab and never creates another.
        foreach(Window window in Application.Current.Windows)
        {
            if(window is MainWindow workspace)
            {
                await workspace.Dispatcher.InvokeAsync(
                    workspace.ShowExcelMappingFromWheelV49);
                return;
            }
        }
        // Different process: send a command to the existing MainWindow.
        for(int retry=0;retry<5;retry++)
        {
            try
            {
                using var client=new NamedPipeClientStream(".",Pipe,
                    PipeDirection.Out,PipeOptions.Asynchronous |
                    PipeOptions.CurrentUserOnly);
                using var timeout=new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(450));
                await client.ConnectAsync(timeout.Token);
                using var writer=new StreamWriter(client){AutoFlush=true};
                await writer.WriteLineAsync("EXCEL_MAPPING");
                return;
            }
            catch(TimeoutException){}
            catch(OperationCanceledException){}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
            await Task.Delay(120);
        }
        // Only if the user has not opened DeskSpace Workspace yet.
        string executable=Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe");
        if(!File.Exists(executable))
            throw new FileNotFoundException(
                "DeskSpace Workspace executable was not found.",executable);
        var child=Process.Start(new ProcessStartInfo(executable){
            UseShellExecute=true,Arguments="--excel-mapping"
        });
        if(child==null)
            throw new IOException("Could not open DeskSpace Workspace.");
    }
}
