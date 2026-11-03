using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using DeskSpace;

static class Program
{
    static int tests;
    static void Check(bool condition,string why)
    {
        if(!condition)throw new Exception("R361 FAIL: "+why);
        Console.WriteLine("R361 PASS: "+why);
        tests++;
    }
    static string Unique(string suffix)=>
        "Local\\DeskSpaceWidgetR361Test."+suffix+"."+Guid.NewGuid().ToString("N");
    static string Pipe()=>
        "DeskSpace.R361.Test."+Guid.NewGuid().ToString("N");

    static async Task<int> Main()
    {
        // Fresh widget start: user has no other process. The existing
        // constructor owns the mutex immediately and does not need fallback.
        using(var fresh=new Mutex(true,Unique("fresh"),out bool created))
        {
            Check(created,"fresh widget claims the only startup mutex");
            fresh.ReleaseMutex();
        }

        // A previous process can leave a named mutex object alive with no
        // owner. The old code used 'createdNew == false' to exit silently.
        var staleName=Unique("unowned");
        using(var leftoverHandle=new Mutex(false,staleName,out bool invented))
        using(var candidate=new Mutex(true,staleName,out bool created))
        {
            Check(invented && !created,"stale existing named mutex reproduces old startup failure");
            var result=DeskSpaceWidgetStartupV361.ResolveExisting(candidate,Pipe(),2,10,0);
            Check(result==DeskSpaceWidgetStartupV361.Outcome.BecameOwner,
                "unowned existing mutex is reclaimed for a real widget");
            candidate.ReleaseMutex();
        }

        // An existing widget owns the mutex and its named pipe accepts Show.
        var normal=Unique("existing");
        var normalPipe=Pipe();
        using(var ready=new ManualResetEventSlim())
        using(var release=new ManualResetEventSlim())
        using(var pipeReady=new ManualResetEventSlim())
        {
            var owner=new Thread(()=>{
                using var held=new Mutex(false,normal);
                held.WaitOne();
                ready.Set();
                release.Wait(TimeSpan.FromSeconds(8));
                held.ReleaseMutex();
            }){IsBackground=true};
            owner.Start();
            Check(ready.Wait(TimeSpan.FromSeconds(3)),"existing widget acquired mutex on another thread");
            Task<int> receiver=Task.Run(()=>{
                using var server=new NamedPipeServerStream(
                    normalPipe,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.None);
                pipeReady.Set();
                server.WaitForConnection();
                return server.ReadByte();
            });
            Check(pipeReady.Wait(TimeSpan.FromSeconds(3)),"existing widget command pipe ready");
            using(var candidate=new Mutex(true,normal,out var created))
            {
                Check(!created,"secondary widget recognized existing singleton");
                var result=DeskSpaceWidgetStartupV361.ResolveExisting(
                    candidate,normalPipe,4,150,20);
                Check(result==DeskSpaceWidgetStartupV361.Outcome.SignaledExisting,
                    "secondary widget forwards command instead of opening a duplicate");
                Check(await receiver.WaitAsync(TimeSpan.FromSeconds(3))==1,
                    "original Show Widget command byte retained");
            }
            release.Set();
            Check(owner.Join(3000),"primary widget owner can release the singleton");
        }

        // Real logon race: another app acquired the mutex but will create
        // the named pipe some milliseconds later. The old Connect(400) lost
        // the request; the new bounded retries must reach the existing host.
        var slowName=Unique("slow");
        var slowPipe=Pipe();
        using(var ready=new ManualResetEventSlim())
        using(var release=new ManualResetEventSlim())
        {
            var owner=new Thread(()=>{
                using var held=new Mutex(false,slowName);
                held.WaitOne();ready.Set();
                release.Wait(TimeSpan.FromSeconds(8));
                held.ReleaseMutex();
            }){IsBackground=true};
            owner.Start();
            Check(ready.Wait(TimeSpan.FromSeconds(3)),"slow primary holds singleton while initializing");
            var receiver=Task.Run(async ()=>{
                await Task.Delay(680);
                using var server=new NamedPipeServerStream(
                    slowPipe,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.None);
                await server.WaitForConnectionAsync();
                return server.ReadByte();
            });
            using(var candidate=new Mutex(true,slowName,out bool created))
            {
                Check(!created,"secondary starts while first widget is initializing");
                var result=DeskSpaceWidgetStartupV361.ResolveExisting(
                    candidate,slowPipe,8,210,75);
                Check(result==DeskSpaceWidgetStartupV361.Outcome.SignaledExisting,
                    "slow primary receives Show after its control pipe starts");
                Check(await receiver.WaitAsync(TimeSpan.FromSeconds(4))==1,
                    "late pipe receives exactly the existing Show byte");
            }
            release.Set();
            Check(owner.Join(3000),"slow owner released normally");
        }

        // Unresponsive owner must not be killed or bypassed. The App may
        // show a recovery message but never opens a second real widget.
        var blocked=Unique("blocked");
        using(var ready=new ManualResetEventSlim())
        using(var release=new ManualResetEventSlim())
        {
            var owner=new Thread(()=>{
                using var held=new Mutex(false,blocked);
                held.WaitOne();ready.Set();
                release.Wait(TimeSpan.FromSeconds(8));
                held.ReleaseMutex();
            }){IsBackground=true};
            owner.Start();
            Check(ready.Wait(TimeSpan.FromSeconds(3)),"unresponsive widget owner exists");
            using(var candidate=new Mutex(true,blocked,out bool created))
            {
                Check(!created,"blocked new widget has not become singleton");
                var result=DeskSpaceWidgetStartupV361.ResolveExisting(
                    candidate,Pipe(),3,40,10);
                Check(result==DeskSpaceWidgetStartupV361.Outcome.UnresponsiveExisting,
                    "unresponsive primary cannot be replaced by second widget");
                Check(!candidate.WaitOne(0),"owner is still protected; new startup does not seize it");
            }
            release.Set();
            Check(owner.Join(3000),"unresponsive owner remains unchanged by election");
        }

        // A stale owner that goes away during handoff can be claimed,
        // with no force-close and no competing widget.
        var released=Unique("released");
        using(var ready=new ManualResetEventSlim())
        {
            var owner=new Thread(()=>{
                using var held=new Mutex(false,released);
                held.WaitOne();ready.Set();
                Thread.Sleep(300);
                held.ReleaseMutex();
            }){IsBackground=true};
            owner.Start();
            Check(ready.Wait(TimeSpan.FromSeconds(3)),"old owner starts then exits on its own");
            using var candidate=new Mutex(true,released,out bool created);
            Check(!created,"candidate sees mutex from previous widget");
            var result=DeskSpaceWidgetStartupV361.ResolveExisting(
                candidate,Pipe(),8,60,35);
            Check(result==DeskSpaceWidgetStartupV361.Outcome.BecameOwner,
                "startup takes over only after previous owner voluntarily exits");
            candidate.ReleaseMutex();
            Check(owner.Join(3000),"previous widget exit completed");
        }
        Console.WriteLine("R361 ALL "+tests+" WINDOWS MUTEX/NAMED PIPE STARTUP TESTS PASSED.");
        return 0;
    }
}
