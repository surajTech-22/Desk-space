using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Excel automation is COM / STA. Running the long, potentially thousands-of-
// calls copy on the WPF UI dispatcher freezes scrolling, keyboard and the ring.
// Read only the selected range from an ALREADY OPEN Excel instance, on a short-
// lived dedicated STA worker. Never create, close, save or alter a workbook.
internal static class DeskSpaceExcelWorkerV41
{
    private static Task<T> Sta<T>(Func<T> read)
    {
        var result=new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{
            try
            {
                // Excel sometimes rejects COM calls while recalculating or
                // showing a modal dialog. Briefly retry those transient cases.
                for(int attempt=0;attempt<4;attempt++)
                {
                    try { result.TrySetResult(read());return; }
                    catch(COMException ex) when(
                        ((uint)ex.HResult==0x80010001 ||
                         (uint)ex.HResult==0x8001010A ||
                         (uint)ex.HResult==0x800AC472) && attempt<3)
                    {
                        Thread.Sleep(180*(attempt+1));
                    }
                }
                result.TrySetException(new InvalidOperationException(
                    "Excel is busy. Finish its open dialog and try again."));
            }
            catch(Exception ex){result.TrySetException(ex);}
        }) { IsBackground=true,Name="DeskSpace Excel read (STA)" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
    public static Task<DeskSpaceExcelTemplateV16> CaptureAsync(
        string title,string range,bool formulas) =>
        Sta(()=>DeskSpaceExcelBridgeV16.Capture(title,range,formulas));

    public static Task<DeskSpaceExcelTemplateV16?> CopyEventAsync()=>
        Sta<DeskSpaceExcelTemplateV16?>(()=>
            DeskSpaceExcelBridgeV16.ExcelCopyActive()
            ? DeskSpaceExcelBridgeV16.Capture(
                "Copied Excel cells · "+DateTime.Now.ToString("HH:mm:ss"),
                "",true)
            : null);

    public static Task<DeskSpaceExcelProposalV16> PreviewAsync(
        DeskSpaceExcelTemplateV16 entry,bool selection,bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Preview(entry,selection,overwrite));

    public static Task<string> ApplyAsync(
        DeskSpaceExcelProposalV16 preview,DeskSpaceExcelTemplateV16 entry,
        bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Apply(preview,entry,overwrite));
}
