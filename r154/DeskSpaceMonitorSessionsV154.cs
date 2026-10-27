using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Independent UI sessions per physical display. Do not clone any WorkspaceStore
// content. All instances run in ONE process and use its existing shared notes,
// mappings, settings, app pins, vault, and clipboard services. Only screen
// coordinates belong to one session and must never overwrite another's.
internal static class DeskSpaceMonitorSessionsV154
{
    private sealed class SavedPosition
    {
        public string Device {get;set;}="";
        public int X {get;set;}
        public int Y {get;set;}
    }
    internal sealed class Session
    {
        public string Device="";
        public bool Primary;
        public WidgetShellWindow Widget=null!;
        public DispatcherTimer? Guard;
    }
    private static readonly Dictionary<WidgetShellWindow,Session> Sessions=new();
    private static readonly object FileGate=new();
    private static string Filename=>Path.Combine(WorkspaceStore.Root,
        "widget-independent-displays-v154.json");

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int L,T,R,B; }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,
        int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")]
    private static extern uint GetCurrentThreadId();
    private delegate bool EnumThreadProc(IntPtr hwnd,IntPtr lparam);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(uint threadId,
        EnumThreadProc callback,IntPtr lparam);

    private const uint NoSize=0x0001,NoZOrder=0x0004,NoActivate=0x0010;
    private const uint Owner=4;

    internal static void Attach(WidgetShellWindow widget,string device,bool primary)
    {
        if(Sessions.TryGetValue(widget,out var existing))
        {
            existing.Device=device;
            return;
        }
        var session=new Session{Widget=widget,Device=device,Primary=primary};
        Sessions.Add(widget,session);
        // This timer is only a fallback for otherwise-untracked WPF Popup HWNDs;
        // tracked popup and widget HWNDs already have synchronous WPF/Win32 hooks.
        var guard=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher)
            {Interval=TimeSpan.FromMilliseconds(250)};
        guard.Tick+=(_,_)=>{if(widget.IsVisible)GuardOwnedWindows(widget);};
        session.Guard=guard;
        guard.Start();
        widget.Closed+=(_,_)=>{
            guard.Stop();
            Sessions.Remove(widget);
        };
    }

    internal static void PrepareExpansion(WidgetShellWindow widget,double targetDiameterDips)
    {
        if(!Sessions.TryGetValue(widget,out var session))return;
        var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName.Equals(
            session.Device,StringComparison.OrdinalIgnoreCase));
        if(screen==null)return;
        var hwnd=new WindowInteropHelper(widget).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect))return;
        var source=System.Windows.PresentationSource.FromVisual(widget);
        double scaleX=source?.CompositionTarget?.TransformToDevice.M11??1;
        double scaleY=source?.CompositionTarget?.TransformToDevice.M22??1;
        var desired=new Rectangle(rect.L,rect.T,
            Math.Max(rect.R-rect.L,(int)Math.Ceiling(targetDiameterDips*scaleX)),
            Math.Max(rect.B-rect.T,(int)Math.Ceiling(targetDiameterDips*scaleY)));
        var fitted=DeskSpaceMonitorFenceV153.Constrain(desired,screen.WorkingArea);
        if(fitted.X!=rect.L||fitted.Y!=rect.T)
            SetWindowPos(hwnd,IntPtr.Zero,fitted.X,fitted.Y,0,0,
                NoSize|NoZOrder|NoActivate);
    }

    internal static bool IsSecondary(WidgetShellWindow widget)=>
        Sessions.TryGetValue(widget,out var s)&&!s.Primary;

    internal static string? Device(WidgetShellWindow widget)=>
        Sessions.TryGetValue(widget,out var s)?s.Device:null;

    internal static bool SavePosition(WidgetShellWindow widget,bool expanded)
    {
        if(!Sessions.TryGetValue(widget,out var s))return false;
        if(!expanded)
        {
            var hwnd=new WindowInteropHelper(widget).Handle;
            if(hwnd!=IntPtr.Zero&&GetWindowRect(hwnd,out var rect))
            {
                var position=new SavedPosition{Device=s.Device,X=rect.L,Y=rect.T};
                lock(FileGate)
                {
                    try
                    {
                        var all=ReadAll();
                        all.RemoveAll(p=>p.Device.Equals(s.Device,
                            StringComparison.OrdinalIgnoreCase));
                        all.Add(position);
                        Directory.CreateDirectory(WorkspaceStore.Root);
                        var tmp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
                        try
                        {
                            File.WriteAllText(tmp,JsonSerializer.Serialize(all));
                            File.Move(tmp,Filename,true);
                        }
                        finally{if(File.Exists(tmp))File.Delete(tmp);}
                    }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                        or JsonException)
                    {System.Diagnostics.Debug.WriteLine("DeskSpace monitor position: "+ex);}
                }
            }
        }
        // Secondary must never write the legacy primary widget position.
        return !s.Primary;
    }

    internal static void Restore(WidgetShellWindow widget)
    {
        if(!Sessions.TryGetValue(widget,out var s))return;
        var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName.Equals(
            s.Device,StringComparison.OrdinalIgnoreCase));
        if(screen==null)return;
        SavedPosition? saved;
        lock(FileGate){saved=ReadAll().FirstOrDefault(x=>x.Device.Equals(
            s.Device,StringComparison.OrdinalIgnoreCase));}
        if(saved==null)return;
        var hwnd=new WindowInteropHelper(widget).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect))return;
        var bounds=new Rectangle(saved.X,saved.Y,rect.R-rect.L,rect.B-rect.T);
        var fitted=DeskSpaceMonitorFenceV153.Constrain(bounds,screen.WorkingArea);
        SetWindowPos(hwnd,IntPtr.Zero,fitted.X,fitted.Y,0,0,
            NoSize|NoZOrder|NoActivate);
        DeskSpaceMonitorFenceV152.ClampNow(widget);
    }

    private static List<SavedPosition> ReadAll()
    {
        try
        {
            return File.Exists(Filename)?
                JsonSerializer.Deserialize<List<SavedPosition>>(
                    File.ReadAllText(Filename))??new():new();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or JsonException)
        {
            System.Diagnostics.Debug.WriteLine("DeskSpace display layouts: "+ex);
            return new();
        }
    }

    // WPF creates several Popup HWNDs which are not always referenced by the
    // same module class. Find windows natively OWNED by this particular widget,
    // not all of DeskSpace's windows. We do not touch Settings or the OTHER widget.
    internal static void GuardOwnedWindows(WidgetShellWindow widget)
    {
        if(!Sessions.TryGetValue(widget,out var s))return;
        var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName.Equals(
            s.Device,StringComparison.OrdinalIgnoreCase));
        if(screen==null)return;
        var root=new WindowInteropHelper(widget).Handle;
        if(root==IntPtr.Zero)return;
        var area=screen.WorkingArea;
        EnumThreadWindows(GetCurrentThreadId(),(hwnd,_)=>{
            if(hwnd==root)return true;
            var current=hwnd;
            bool owned=false;
            for(int n=0;n<12;n++)
            {
                current=GetWindow(current,Owner);
                if(current==IntPtr.Zero)break;
                if(current==root){owned=true;break;}
            }
            if(!owned)return true;
            if(!GetWindowRect(hwnd,out var rect))return true;
            var original=new Rectangle(rect.L,rect.T,rect.R-rect.L,rect.B-rect.T);
            if(original.Width<=0||original.Height<=0)return true;
            var fit=DeskSpaceMonitorFenceV153.Constrain(original,area);
            if(fit!=original)
                SetWindowPos(hwnd,IntPtr.Zero,fit.X,fit.Y,fit.Width,fit.Height,
                    NoZOrder|NoActivate);
            DeskSpaceNativeClipV154.Apply(hwnd,area);
            return true;
        },IntPtr.Zero);
    }
}
