using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Formats NEVER use the information/header-value paste writer.
// All Microsoft Excel COM operations are executed on an STA worker.
// The caller reviews and explicitly approves an unchanged destination.
internal static class DeskSpaceExcelFormatWorkerV130
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static async Task<T> OnSta<T>(Func<T> operation)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var done=new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread=new Thread(()=>{
                try
                {
                    for(int attempt=0;attempt<4;attempt++)
                    {
                        try{done.TrySetResult(operation());return;}
                        catch(COMException ex) when(
                            ((uint)ex.HResult==0x80010001 ||
                             (uint)ex.HResult==0x8001010A ||
                             (uint)ex.HResult==0x800AC472) && attempt<3)
                        {Thread.Sleep(180*(attempt+1));}
                    }
                    done.TrySetException(new InvalidOperationException(
                        "Excel is busy. Finish its dialog and retry."));
                }
                catch(Exception ex){done.TrySetException(ex);}
            }){IsBackground=true,Name="DeskSpace format review (STA)"};
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return await done.Task.ConfigureAwait(false);
        }
        finally{Gate.Release();}
    }
    internal static Task<DeskSpaceExcelFormatApplyV109.Review> PreviewAsync(
        DeskSpaceExcelTemplateV16 template)=>
        OnSta(()=>DeskSpaceExcelFormatApplyV109.Preview(template));
    internal static Task<string> ApplyAsync(
        DeskSpaceExcelTemplateV16 template,
        DeskSpaceExcelFormatApplyV109.Review approval)=>
        OnSta(()=>DeskSpaceExcelFormatApplyV109.Apply(template,approval));
}
