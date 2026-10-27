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
    internal static void Register(WidgetShellWindow widget)
    {
        if(creatingSatellite || IsSatellite(widget) || primary!=null)return;
        primary=widget;
        ReadPreference();
        poll=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher){
            Interval=TimeSpan.FromSeconds(3)};
        poll.Tick+=(_,_)=>{
            if(primary==null)return;
            try
            {
                var stamp=File.Exists(Filename)?File.GetLastWriteTimeUtc(Filename):
                    DateTime.MinValue;
                if(stamp!=diskStamp)ReadPreference();
                Reconcile();
            }
            catch(Exception error){Log(error);}
        };
        poll.Start();
        SystemEvents.DisplaySettingsChanged+=OnDisplaysChanged;
        widget.Closed+=(_,_)=>{
            poll?.Stop();poll=null;
            SystemEvents.DisplaySettingsChanged-=OnDisplaysChanged;
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
            var original=Forms.Screen.FromHandle(new WindowInteropHelper(primary).Handle);
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
                        replica.Show();
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
                else PlaceIfOffscreen(replica,screen);
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
        if(!double.IsFinite(replica.Left)||!double.IsFinite(replica.Top))
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
