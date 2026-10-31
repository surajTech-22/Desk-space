using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Separate display-only preference. Never rewrite widget-v3.json, Excel maps,
// notes, app pins or the independent update-channel configuration.
internal static class DeskSpaceMultiMonitorV151
{
    private sealed class Preference { public bool ShowAll {get;set;} }
    private static readonly Dictionary<string,WidgetShellWindow> Satellites=
        new(StringComparer.OrdinalIgnoreCase);
    private static WidgetShellWindow? primary;
    private static DispatcherTimer? poll;
    private static FileSystemWatcher? preferenceWatcherV216;
    // Sleep/resume can expose stale virtual-screen topology for several
    // seconds. Short, finite retries only, never an additional polling loop.
    private static readonly int[] ResumeDelaysV247={1500,5000,12000};
    private static DispatcherTimer? resumeTimerV247;
    private static int resumeAttemptV247;
    internal static int ResumeDelayForAttemptV247(int attempt)=>
        attempt>=0 && attempt<ResumeDelaysV247.Length?
            ResumeDelaysV247[attempt]:-1;
    private static DispatcherTimer? preferenceDebounceV216;
    private static readonly TimeSpan ReconcileFallbackV216=TimeSpan.FromSeconds(45);
    private static bool creatingSatellite, updating, enabled;
    private static DateTime diskStamp=DateTime.MinValue;
    private static bool initialized;
    private static string Filename=>Path.Combine(WorkspaceStore.Root,
        "widget-displays-v151.json");

    internal static bool Enabled
    {
        get
        {
            if(!initialized)ReadPreference();
            return enabled;
        }
    }
    // Consulted during satellite construction, before the new HWND is shown.
    internal static bool ConstructingSatellite=>creatingSatellite;
    internal static bool IsSatellite(WidgetShellWindow widget)
        => creatingSatellite || Satellites.Values.Any(w=>ReferenceEquals(w,widget));

    private static void ReadPreference()
    {
        try
        {
            enabled=File.Exists(Filename) &&
                (JsonSerializer.Deserialize<Preference>(File.ReadAllText(Filename))?.ShowAll??false);
            diskStamp=File.Exists(Filename)?File.GetLastWriteTimeUtc(Filename):DateTime.MinValue;
        }
        catch(Exception error)
        {
            System.Diagnostics.Debug.WriteLine("Monitor preference: "+error);
            enabled=false;
        }
        initialized=true;
    }
    internal static void Configure(bool showAll)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var temp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(new Preference{ShowAll=showAll}));
            File.Move(temp,Filename,true);
            ReadPreference();
            Reconcile();
        }
        finally { try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){} }
    }

    // Called only by the full widget after its HWND is ready. Satellites reuse
    // the original WidgetShellWindow, not a fake screenshot/donut.
    // FileSystemWatcher can occasionally overflow or miss an atomic rename.
    // A slow recovery pass remains in place; the normal path is event-driven.
    private static void SchedulePreferenceRefreshV216()
    {
        var owner=primary;
        if(owner==null || owner.Dispatcher.HasShutdownStarted)return;
        try
        {
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(primary==null || !ReferenceEquals(primary,owner))return;
                preferenceDebounceV216?.Stop();
                preferenceDebounceV216?.Start();
            }));
        }
        catch(InvalidOperationException){}
    }
    private static void StartPreferenceWatcherV216()
    {
        if(primary==null || preferenceWatcherV216!=null)return;
        try
        {
            if(!Directory.Exists(WorkspaceStore.Root))return;
            var watcher=new FileSystemWatcher(WorkspaceStore.Root,
                "widget-displays-v151.json"){
                NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|
                    NotifyFilters.CreationTime,
                IncludeSubdirectories=false
            };
            watcher.Changed+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Created+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Deleted+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Renamed+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Error+=(_,_)=>{
                SchedulePreferenceRefreshV216();
                // Fallback still reads preferences if FileSystemWatcher fails.
                // Do not dispose on the watcher callback thread; the next
                // dispatcher tick cleans up and may recreate it.
                var owner=primary;
                if(owner==null || owner.Dispatcher.HasShutdownStarted)return;
                try{owner.Dispatcher.BeginInvoke(new Action(()=>{
                    preferenceWatcherV216?.Dispose();
                    preferenceWatcherV216=null;
                }));}
                catch(InvalidOperationException){}
            };
            watcher.EnableRaisingEvents=true;
            preferenceWatcherV216=watcher;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Debug.WriteLine(
                "DeskSpace display preference signal unavailable: "+ex.GetType().Name);
        }
    }
    private static void StopPreferenceWatcherV216()
    {
        preferenceDebounceV216?.Stop();
        preferenceDebounceV216=null;
        preferenceWatcherV216?.Dispose();
        preferenceWatcherV216=null;
    }

    // Only the primary widget owns the recovery subscription. Do not change
    // assigned screen identity or copy other monitor's Deck positions.
    private static void OnPowerModeV247(object? sender,PowerModeChangedEventArgs args)
    {
        if(args.Mode!=PowerModes.Resume)return;
        var owner=primary;
        if(owner==null || owner.Dispatcher.HasShutdownStarted)return;
        try
        {
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(!ReferenceEquals(primary,owner))return;
                resumeAttemptV247=0;
                resumeTimerV247?.Stop();
                if(resumeTimerV247==null)
                {
                    resumeTimerV247=new DispatcherTimer(
                        DispatcherPriority.Background,owner.Dispatcher);
                    resumeTimerV247.Tick+=(_,_)=>{
                        var timer=resumeTimerV247;
                        timer?.Stop();
                        if(primary==null){resumeAttemptV247=0;return;}
                        try
                        {
                            // Re-read the saved display preference and then
                            // resolve current screens. Reconcile preserves
                            // per-display identity and independent positions.
                            ReadPreference();
                            Reconcile();
                        }
                        catch(Exception error){Log(error);}
                        resumeAttemptV247++;
                        int next=ResumeDelayForAttemptV247(resumeAttemptV247);
                        if(next>0 && resumeTimerV247!=null && primary!=null)
                        {
                            resumeTimerV247.Interval=TimeSpan.FromMilliseconds(next);
                            resumeTimerV247.Start();
                        }
                    };
                }
                resumeTimerV247.Interval=TimeSpan.FromMilliseconds(
                    ResumeDelayForAttemptV247(0));
                resumeTimerV247.Start();
            }));
        }
        catch(InvalidOperationException){}
    }

    internal static void Register(WidgetShellWindow widget)
    {
        if(creatingSatellite || IsSatellite(widget) || primary!=null)return;
        primary=widget;
        DeskSpaceMonitorFenceV152.Bind(widget,
            Forms.Screen.FromHandle(new WindowInteropHelper(widget).Handle).DeviceName);
        DeskSpaceMonitorSessionsV154.Attach(widget,
            Forms.Screen.FromHandle(new WindowInteropHelper(widget).Handle).DeviceName,
            true);
        ReadPreference();
        preferenceDebounceV216=new DispatcherTimer(
            DispatcherPriority.Background,widget.Dispatcher){
            Interval=TimeSpan.FromMilliseconds(180)};
        preferenceDebounceV216.Tick+=(_,_)=>{
            preferenceDebounceV216?.Stop();
            if(primary==null)return;
            try
            {
                ReadPreference();
                Reconcile();
            }
            catch(Exception error){Log(error);}
        };
        StartPreferenceWatcherV216();
        poll=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher){
            Interval=ReconcileFallbackV216};
        poll.Tick+=(_,_)=>{
            if(primary==null)return;
            try
            {
                if(preferenceWatcherV216==null)StartPreferenceWatcherV216();
                var stamp=File.Exists(Filename)?File.GetLastWriteTimeUtc(Filename):
                    DateTime.MinValue;
                if(stamp!=diskStamp)ReadPreference();
                // Rare missed notification, owner close or monitor reconnect:
                // keep this slow check as a safety net, not the normal path.
                Reconcile();
            }
            catch(Exception error){Log(error);}
        };
        poll.Start();
        SystemEvents.DisplaySettingsChanged+=OnDisplaysChanged;
        SystemEvents.PowerModeChanged+=OnPowerModeV247;
        widget.Closed+=(_,_)=>{
            poll?.Stop();poll=null;
            StopPreferenceWatcherV216();
            SystemEvents.DisplaySettingsChanged-=OnDisplaysChanged;
            SystemEvents.PowerModeChanged-=OnPowerModeV247;
            resumeTimerV247?.Stop();
            resumeTimerV247=null;
            resumeAttemptV247=0;
            CloseAll();
            primary=null;
        };
        Reconcile();
    }
    private static void OnDisplaysChanged(object? sender,EventArgs args)
    {
        var widget=primary;
        if(widget==null)return;
        try{widget.Dispatcher.BeginInvoke(new Action(Reconcile));}
        catch(Exception error){Log(error);}
    }
    private static void Reconcile()
    {
        if(updating||primary==null)return;
        if(!primary.Dispatcher.CheckAccess()){
            primary.Dispatcher.BeginInvoke(new Action(Reconcile));return;
        }
        updating=true;
        try
        {
            var screens=Forms.Screen.AllScreens;
            // Do not change the primary assignment when dragging near the seam.
            var original=DeskSpaceMonitorFenceV152.BoundScreen(primary) ??
                Forms.Screen.FromHandle(new WindowInteropHelper(primary).Handle);
            DeskSpaceMonitorSessionsV154.Attach(primary,original.DeviceName,true);
            // Reconcile is also triggered by DisplaySettingsChanged: move a
            // reconnected primary back inside its originally assigned monitor.
            // No cross-monitor UI state or shared Deck contents are altered.
            // Do not re-clamp a user-positioned primary every recovery tick.
            // Its HWND fence already prevents real monitor escapes, and
            // this relocation is needed only after a topology change.
            var primaryHwnd=new WindowInteropHelper(primary).Handle;
            if(primaryHwnd!=IntPtr.Zero && !string.Equals(
                Forms.Screen.FromHandle(primaryHwnd).DeviceName,
                original.DeviceName,StringComparison.OrdinalIgnoreCase))
                DeskSpaceMonitorFenceV152.ClampNow(primary);
            var wanted=enabled && screens.Length>1?
                screens.Where(x=>x.DeviceName!=original.DeviceName).ToArray():
                Array.Empty<Forms.Screen>();
            var names=new HashSet<string>(wanted.Select(x=>x.DeviceName),
                StringComparer.OrdinalIgnoreCase);
            foreach(var stale in Satellites.Keys.Where(x=>!names.Contains(x)).ToArray())
                Remove(stale);
            foreach(var screen in wanted)
            {
                if(!Satellites.TryGetValue(screen.DeviceName,out var replica))
                {
                    try
                    {
                        creatingSatellite=true;
                        replica=new WidgetShellWindow
                        {
                            WindowStartupLocation=WindowStartupLocation.Manual
                        };
                        Satellites[screen.DeviceName]=replica;
                        Place(replica,screen);
                        // Reposition in native pixels AFTER WPF creates the HWND.
                        // This is safe even with different per-monitor DPI scaling.
                        // Construct a real native HWND while still hidden, then
                        // attach its OWN screen/session before its first paint.
                        // This avoids flashing on the primary monitor and avoids
                        // the first WPF show placing it across the seam.
                        new WindowInteropHelper(replica).EnsureHandle();
                        DeskSpaceMonitorFenceV152.PlaceNewSatellite(replica,screen);
                        DeskSpaceMonitorSessionsV154.Attach(replica,screen.DeviceName,false);
                        DeskSpaceMonitorSessionsV154.Restore(replica);
                        replica.Show();
                        DeskSpaceMonitorFenceV152.ClampNow(replica);
                        replica.Closed+=(_,_)=>{
                            if(Satellites.TryGetValue(screen.DeviceName,out var existing)
                               && ReferenceEquals(existing,replica))
                                Satellites.Remove(screen.DeviceName);
                        };
                    }
                    catch(Exception error)
                    {
                        if(replica!=null)
                        {
                            try{replica.Close();}catch(Exception closeError){Log(closeError);}
                            Satellites.Remove(screen.DeviceName);
                        }
                        Log(error);
                    }
                    finally{creatingSatellite=false;}
                }
                else
                {
                    // Existing independent secondary positions must never
                    // be rewritten during an ordinary 45-second recovery
                    // pass. Correct only if Windows has moved the HWND to
                    // another physical device; normal movement is handled
                    // by the per-window native HWND fence.
                    var hwnd=new WindowInteropHelper(replica).Handle;
                    if(hwnd!=IntPtr.Zero && !string.Equals(
                        Forms.Screen.FromHandle(hwnd).DeviceName,
                        screen.DeviceName,StringComparison.OrdinalIgnoreCase))
                        DeskSpaceMonitorFenceV152.ClampNow(replica);
                }
            }
        }
        finally{updating=false;}
    }
    private static void Place(WidgetShellWindow replica,Forms.Screen screen)
    {
        // All native Windows screen coordinates are in virtual-desktop space.
        // WPF uses DIPs; use the target HWND's WPF transform when available.
        double scaleX=1,scaleY=1;
        var source=PresentationSource.FromVisual(replica);
        if(source?.CompositionTarget!=null)
        {
            scaleX=source.CompositionTarget.TransformFromDevice.M11;
            scaleY=source.CompositionTarget.TransformFromDevice.M22;
        }
        var area=screen.WorkingArea;
        replica.Left=(area.Right-24)*scaleX-replica.Width;
        replica.Top=(area.Top+32)*scaleY;
    }
    private static void PlaceIfOffscreen(WidgetShellWindow replica,Forms.Screen screen)
    {
        if(!double.IsFinite(replica.Left)||!double.IsFinite(replica.Top) ||
           !string.Equals(Forms.Screen.FromHandle(
                new WindowInteropHelper(replica).Handle).DeviceName,
                screen.DeviceName,StringComparison.OrdinalIgnoreCase))
            Place(replica,screen);
    }
    private static void Remove(string deviceName)
    {
        if(!Satellites.TryGetValue(deviceName,out var widget))return;
        // Keep the satellite registered throughout Closing. Otherwise its
        // original save-position handler would overwrite the primary position.
        try{widget.Close();}catch(Exception error){Log(error);}
        finally{Satellites.Remove(deviceName);}
    }
    private static void CloseAll()
    {
        foreach(var name in Satellites.Keys.ToArray())Remove(name);
    }
    private static void Log(Exception error)
    {
        System.Diagnostics.Debug.WriteLine("DeskSpace display mirror: "+error);
        try
        {
            var dir=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),"Downloads");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir,"DeskSpace_Monitor_Errors.log"),
                "["+DateTime.Now.ToString("O")+"] "+error+Environment.NewLine);
        }
        catch(Exception logError){System.Diagnostics.Debug.WriteLine(logError);}
    }
}
