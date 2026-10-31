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
    private static readonly SemaphoreSlim ExcelGate=new(1,1);
    private static async Task<T> Sta<T>(Func<T> read)
    {
        await ExcelGate.WaitAsync().ConfigureAwait(false);
        try
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
            catch(Exception ex)
            {
                // Log exception type/HRESULT/call location; never log captured
                // cell data. Continue to surface exact original error to UI.
                try
                {
                    var downloads=System.IO.Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile),"Downloads");
                    System.IO.Directory.CreateDirectory(downloads);
                    var path=System.IO.Path.Combine(downloads,
                        "DeskSpace_Excel_COM_Error.log");
                    var details=DateTime.Now.ToString("o")+" | "+
                        ex.GetType().FullName+" | 0x"+
                        ex.HResult.ToString("X8")+" | "+
                        ex.Message+Environment.NewLine+
                        ex.StackTrace+Environment.NewLine+
                        "------------------------------"+Environment.NewLine;
                    System.IO.File.AppendAllText(path,details);
                }
                catch(System.IO.IOException){}
                catch(UnauthorizedAccessException){}
                result.TrySetException(ex);
            }
        }) { IsBackground=true,Name="DeskSpace Excel read (STA)" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await result.Task.ConfigureAwait(false);
        }
        finally {ExcelGate.Release();}
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

    // Format-only operations are separate from the existing values-only autofill.
    public static Task<string> PasteSpecialFormulasAsync(
        DeskSpaceExcelTemplateV16 entry)=>
        Sta(()=>DeskSpaceExcelPasteSpecialV144.Paste(entry));

    public static Task<DeskSpaceExcelFormatApplyV109.Review> PreviewFormatAsync(
        DeskSpaceExcelTemplateV16 entry)=>
        Sta(()=>DeskSpaceExcelFormatApplyV109.Preview(entry));

    public static Task<string> ApplyFormatAsync(
        DeskSpaceExcelTemplateV16 entry,
        DeskSpaceExcelFormatApplyV109.Review approved)=>
        Sta(()=>DeskSpaceExcelFormatApplyV109.Apply(entry,approved));

    public static Task<string> ApplyAsync(
        DeskSpaceExcelProposalV16 preview,DeskSpaceExcelTemplateV16 entry,
        bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Apply(preview,entry,overwrite));

    internal static Task<DeskSpaceExcelNormalizePlanV102> ReviewTypesAsync(
        DeskSpaceExcelTemplateV16 model,string dateOrder)=>
        Sta(()=>DeskSpaceExcelNormalizeV102.Preview(model,dateOrder));

    internal static Task<string> RepairTypesAsync(
        DeskSpaceExcelNormalizePlanV102 plan,Guid mappingId)=>
        Sta(()=>DeskSpaceExcelNormalizeV102.Apply(plan,mappingId));
}
