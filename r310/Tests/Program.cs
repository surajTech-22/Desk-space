using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

internal static class Program
{
    private static void Check(bool ok,string msg)
    {
        if(!ok)throw new Exception("R310 Windows mutex regression FAILED: "+msg);
    }

    [STAThread]
    private static int Main()
    {
        var assembly=typeof(DeskSpace.WeatherReading).Assembly;
        var type=assembly.GetType("DeskSpace.DeskSpacePublicUpdaterV44",true)!;
        var handoff=type.GetMethod("StartSupervisor",BindingFlags.NonPublic|BindingFlags.Static)!;
        using var ready=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        Exception? holderError=null;
        bool owned=false;
        var worker=new Thread(()=>{
            try
            {
                using var mutex=new Mutex(false,@"Local\DeskSpace-UpdateSupervisor-V185");
                owned=mutex.WaitOne(0);
                ready.Set();
                release.Wait(TimeSpan.FromSeconds(20));
                if(owned)mutex.ReleaseMutex();
            }
            catch(Exception ex){holderError=ex;ready.Set();}
        }) {IsBackground=true};
        worker.Start();
        try
        {
            Check(ready.Wait(TimeSpan.FromSeconds(10)),
                "separate thread did not start mutex owner");
            Check(owned && holderError==null,
                "could not acquire native installer mutex for test");
            // The guard runs before staging a script or starting a process.
            // Use nonexistent paths to ensure this test CANNOT execute an installer.
            var fake=Path.Combine(Path.GetTempPath(),"R310-DO-NOT-INSTALL.exe");
            var folder=Path.Combine(Path.GetTempPath(),"R310-DO-NOT-INSTALL");
            bool busy=false;
            try
            {
                var task=(Task)handoff.Invoke(null,new object[]{fake,folder,"v1.5.1112"})!;
                task.GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                var actual=ex is TargetInvocationException tie ? tie.InnerException??ex:ex;
                busy=actual.GetType().Name=="SupervisorBusyV310Exception";
            }
            Check(busy,"existing owner must defer, never launch second installer");
            Check(!Directory.Exists(folder),
                "duplicate installer attempt created staging directory");
            Check(worker.IsAlive,
                "second attempt interrupted the genuine installer owner");
            release.Set();
            Check(worker.Join(TimeSpan.FromSeconds(10)),
                "test supervisor owner did not release Windows mutex");
            using var probe=new Mutex(false,@"Local\DeskSpace-UpdateSupervisor-V185");
            bool acquired=false;
            try{acquired=probe.WaitOne(0);}
            catch(AbandonedMutexException){acquired=true;}
            Check(acquired,"supervisor mutex did not become free when owner exited");
            if(acquired)probe.ReleaseMutex();
            Console.WriteLine("R310 PASS: existing live supervisor yields benign retry; no installer process, no duplicate handoff, no lock deletion, mutex auto-released after owner exit.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{release.Set();worker.Join(TimeSpan.FromSeconds(4));}
    }
}
