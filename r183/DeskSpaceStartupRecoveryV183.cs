using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace DeskSpace;

// Startup-only crash-loop recovery. This is NOT a complete crash watchdog:
// crashes before the primary widget loads or after the startup grace period
// require separate diagnostics. Never deletes or rewrites workspace data.
// After three failed *short* starts within ten minutes, the next launch
// retains one primary widget/tray but skips satellite widget creation.
// Recovery is sticky until the user explicitly opts back in.
internal static class DeskSpaceStartupRecoveryV183
{
    internal sealed record State(
        DateTime StartedUtc,int ShortFailures,bool Healthy,bool SafeMode);

    private const int Threshold=3;
    private static readonly TimeSpan LoopWindow=TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HealthyAfter=TimeSpan.FromSeconds(45);
    private static readonly object Gate=new();
    private static readonly string StateFile=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","Recovery","startup-v1.json");

    private static bool started,safeMode,tracking;
    private static State? session;
    internal static bool SafeMode=>safeMode;
    internal static bool IsTracking=>tracking;
    internal static string Status=>!tracking?
        "Startup recovery unavailable (normal startup retained)":
        safeMode?
        "Recovery mode: secondary widgets paused; data and tray retained":
        "Normal startup; short-crash monitor armed";

    // Pure transition, independently testable without reading user files.
    internal static State NextStart(State? previous,DateTime utcNow)
    {
        int failures=0;
        bool sticky=previous?.SafeMode==true;
        if(previous!=null && !previous.Healthy &&
            previous.StartedUtc.Kind==DateTimeKind.Utc)
        {
            var elapsed=utcNow-previous.StartedUtc;
            if(elapsed>=TimeSpan.Zero && elapsed<=LoopWindow)
                failures=Math.Clamp(previous.ShortFailures,0,Threshold)+1;
        }
        if(failures>=Threshold)sticky=true;
        return new State(utcNow,failures,false,sticky);
    }

    private static State? Read()
    {
        try
        {
            if(!File.Exists(StateFile)||new FileInfo(StateFile).Length>8192)
                return null;
            var entry=JsonSerializer.Deserialize<State>(
                File.ReadAllText(StateFile,Encoding.UTF8));
            if(entry==null||entry.StartedUtc.Kind!=DateTimeKind.Utc||
                entry.ShortFailures<0||entry.ShortFailures>Threshold+1)
                return null;
            return entry;
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException or NotSupportedException)
        {
            Debug.WriteLine("DeskSpace startup recovery read: "+error.GetType().Name);
            return null;
        }
    }

    private static void Persist(State updated)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        string temporary=StateFile+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(updated),
                new UTF8Encoding(false));
            File.Move(temporary,StateFile,true);
        }
        finally
        {
            try{if(File.Exists(temporary))File.Delete(temporary);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

    private static void MarkHealthy()
    {
        lock(Gate)
        {
            if(!tracking||session==null)return;
            try
            {
                session=session with{Healthy=true,ShortFailures=0};
                Persist(session);
            }
            catch(Exception error) when(error is IOException or
                UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Debug.WriteLine("DeskSpace startup recovery write: "+error.GetType().Name);
            }
        }
    }

    internal static void Start(Window owner)
    {
        // Only the primary widget calls Start; satellite windows do not write
        // another session or overwrite the primary crash-loop evidence.
        lock(Gate)
        {
            if(started)return;
            started=true;
            try
            {
                session=NextStart(Read(),DateTime.UtcNow);
                Persist(session); // fail open if the marker cannot be written
                tracking=true;
                safeMode=session.SafeMode;
            }
            catch(Exception error) when(error is IOException or
                UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                tracking=false;safeMode=false;
                Debug.WriteLine("DeskSpace startup recovery unavailable: "+
                    error.GetType().Name);
            }
        }
        var grace=new DispatcherTimer(DispatcherPriority.Background,owner.Dispatcher)
            {Interval=HealthyAfter};
        EventHandler? closed=null;
        closed=(_,_)=>{
            grace.Stop();
            owner.Closed-=closed;
            MarkHealthy(); // ordinary close is never a crash
        };
        grace.Tick+=(_,_)=>{
            grace.Stop();
            MarkHealthy(); // healthy after a sustained start
        };
        owner.Closed+=closed;
        grace.Start();
    }

    // User-controlled recovery. Changes only this guard's private marker;
    // a restart is deliberately required before satellites are re-enabled.
    internal static bool ResumeFullStartupNextLaunch()
    {
        lock(Gate)
        {
            if(!tracking||session==null)return false;
            try
            {
                var candidate=session with{
                    Healthy=true,ShortFailures=0,SafeMode=false};
                Persist(candidate);
                session=candidate;
                return true;
            }
            catch(Exception error) when(error is IOException or
                UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Debug.WriteLine("DeskSpace recovery reset: "+error.GetType().Name);
                return false;
            }
        }
    }
}
