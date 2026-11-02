using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    static void Check(bool ok,string reason)
    {
        if(!ok)throw new InvalidOperationException("R337 search spinner: "+reason);
    }
    static int Main()
    {
        try
        {
            var assembly=typeof(DeskSpace.WeatherReading).Assembly;
            var type=assembly.GetType("DeskSpace.DeskSpaceSearchGateV337",true)!;
            var gate=Activator.CreateInstance(type,true)!;
            const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
            bool Call(string method,params object[] args)=>
                (bool)type.GetMethod(method,F)!.Invoke(gate,args)!;
            bool Active()=>(bool)type.GetProperty("Active",F)!.GetValue(gate)!;

            Check(Call("Begin"),"first UI search could not start");
            Check(Active(),"inflight search not recorded");
            // Simulate full-volume indexing adding new rows every tick while a
            // slow query is in-flight. None of these ticks may start/cancel
            // a second query or the UI will stay on Searching forever.
            for(long i=0;i<1000;i++)
            {
                Check(!Call("RefreshTick",i),
                    "index revision "+i+" incorrectly interrupted in-flight request");
                Check(Active(),"search lost in-flight state during indexing");
            }
            Check(!Call("Begin"),"second disk search began before first finished");
            Check(Call("Finish"),"queued index refresh was lost");
            Check(!Active(),"search remained busy after completion");
            Check(Call("Begin"),"coalesced retry could not begin");
            Check(!Call("RefreshTick",999L),
                "unchanged index triggered a duplicate refresh");
            Check(!Call("Finish"),"repeated refresh generated another work item");
            Check(Call("Begin"),"user typed a new filename but query could not begin");
            type.GetMethod("QueueUserChange",F)!.Invoke(gate,Array.Empty<object>());
            Check(Call("Finish"),"latest user input was not replayed");
            Check(Call("Begin"),"new query after cancellation could not run");
            Check(!Call("Finish"),"no queued request should remain");
            // UI query state is not required to track each filename; bounded
            // controller retains just a revision and one queued flag.
            long bytes=GC.GetTotalMemory(true);
            for(long i=1000;i<101000;i++)
            {
                Check(Call("Begin"),"repeat search failed");
                Call("RefreshTick",i);
                Call("Finish");
            }
            long retained=GC.GetTotalMemory(true)-bytes;
            Check(retained<4*1024*1024,
                "controller retained "+retained+" bytes during 100k refreshes");
            Console.WriteLine("R337 PASS: 1000 index refreshes cannot cancel in-flight search; "+
                "latest user input coalesces once; unchanged revisions skipped; "+
                "100k query cycles use bounded memory.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
