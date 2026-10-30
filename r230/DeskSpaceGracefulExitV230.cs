using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DeskSpace;

// R230: every DeskSpace process (Workspace, floating widgets and tray host)
// listens for ONE installation-scoped graceful-close request. Close raises
// ordinary WPF Closing events and honours cancellation / unsaved-work UI.
internal static class DeskSpaceGracefulExitV230
{
    private static DispatcherTimer? listener;
    private static DateTime lastAttempt=DateTime.MinValue;
    internal static string EventName(string executable)
    {
        string canonical=Path.GetFullPath(executable)
            .TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        string hash=Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical)));
        return @"Local\DeskSpace-UpdateExit-V230-"+hash.Substring(0,24);
    }

    internal static void Attach(Window owner)
    {
        if(listener!=null)return; // singleton per process, not per monitor
        var timer=new DispatcherTimer(DispatcherPriority.Background,owner.Dispatcher)
        {Interval=TimeSpan.FromMilliseconds(400)};
        timer.Tick+=(_,_)=>{
            if(DateTime.UtcNow-lastAttempt<TimeSpan.FromSeconds(8))return;
            try
            {
                using var signal=EventWaitHandle.OpenExisting(EventName(
                    Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe")));
                if(!signal.WaitOne(0))return;
            }
            catch(WaitHandleCannotBeOpenedException){return;}
            catch(UnauthorizedAccessException){return;}
            lastAttempt=DateTime.UtcNow;
            // WPF's actual Close/Closing lifecycle saves Workspace state and
            // permits a window to veto exit. This does NOT call Environment.Exit.
            if(TryCloseWindows(Application.Current.Windows.Cast<Window>().ToArray()))
            {
                timer.Stop();
                Application.Current.Shutdown();
            }
            // If any window refused, keep the app and saved state intact.
            // A later request is allowed, but never every 400 ms.
        };
        listener=timer;
        timer.Start();
    }

    // Testable using real WPF Closing/Closed events. A veto must leave the
    // application running; never use Process.Kill or Environment.Exit.
    internal static bool TryCloseWindows(Window[] windows)=>
        TryCloseInOrder(windows,Application.Current?.MainWindow);

    // Closing the process main window first can trigger ShutdownMode's
    // automatic shutdown BEFORE secondary windows get their chance to
    // reject exit over unsaved work. Close children first; stop at the
    // first veto; close the process main window strictly LAST.
    internal static bool TryCloseInOrder(Window[] windows,Window? main)
    {
        var ordered=windows.Where(w=>!ReferenceEquals(w,main))
            .Concat(windows.Where(w=>ReferenceEquals(w,main))).ToArray();
        foreach(var window in ordered)
        {
            bool closed=false;
            EventHandler handler=(_,_)=>closed=true;
            window.Closed+=handler;
            try{window.Close();}
            catch(InvalidOperationException){return false;}
            finally{window.Closed-=handler;}
            if(!closed)return false;
        }
        return true;
    }
}
