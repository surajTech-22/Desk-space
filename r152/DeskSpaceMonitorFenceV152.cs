using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// One screen per widget. All rectangles below are *native pixels*, not WPF
// DIPs; Windows can use different DPI values on neighbouring monitors.
internal static class DeskSpaceMonitorFenceV152
{
    private static readonly Dictionary<WidgetShellWindow,string> Assigned=new();
    private static readonly HashSet<WidgetShellWindow> Hooked=new();
    private static readonly HashSet<WidgetShellWindow> Moving=new();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left,Top,Right,Bottom;
        public int Width=>Right-Left;
        public int Height=>Bottom-Top;
    }
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect bounds);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr insertAfter,
        int x,int y,int width,int height,uint flags);
    private const uint NoSize=0x0001, NoZOrder=0x0004, NoActivate=0x0010;

    internal static System.Drawing.Point ClampRectangle(Rectangle bounds,Rectangle available)
    {
        // Include the taskbar and negative virtual desktop coordinates.
        int maxX=Math.Max(available.Left,available.Right-bounds.Width);
        int maxY=Math.Max(available.Top,available.Bottom-bounds.Height);
        return new System.Drawing.Point(Math.Clamp(bounds.Left,available.Left,maxX),
                         Math.Clamp(bounds.Top,available.Top,maxY));
    }

    internal static void Bind(WidgetShellWindow window,string screenName)
    {
        if(window.Dispatcher.CheckAccess())
        {
            Assigned[window]=screenName;
            if(Hooked.Add(window))
            {
                window.LocationChanged+=(_,_)=>Queue(window);
                window.SizeChanged+=(_,_)=>Queue(window);
                window.StateChanged+=(_,_)=>Queue(window);
                window.Loaded+=(_,_)=>Queue(window);
                window.Closed+=(_,_)=>{
                    Assigned.Remove(window);
                    Hooked.Remove(window);
                    Moving.Remove(window);
                };
            }
            Queue(window);
        }
        else window.Dispatcher.BeginInvoke(new Action(()=>Bind(window,screenName)));
    }

    internal static Forms.Screen? BoundScreen(WidgetShellWindow window)
    {
        var screens=Forms.Screen.AllScreens;
        if(screens.Length==0)return null;
        if(Assigned.TryGetValue(window,out var name))
        {
            var bound=screens.FirstOrDefault(s=>string.Equals(s.DeviceName,name,
                StringComparison.OrdinalIgnoreCase));
            if(bound!=null)return bound;
        }
        // Monitor disconnected: re-home the original widget, not the entire app.
        var fallback=Forms.Screen.PrimaryScreen??screens[0];
        Assigned[window]=fallback.DeviceName;
        return fallback;
    }

    // Called from WidgetShellWindow's existing KeepOnVisibleDesktop path.
    // Before initial primary-window binding, let original startup logic run.
    internal static bool ClampIfBound(WidgetShellWindow window)
    {
        if(!Assigned.ContainsKey(window))return false;
        Queue(window);
        return true;
    }

    private static void Queue(WidgetShellWindow window)
    {
        if(Moving.Contains(window)||!Assigned.ContainsKey(window)||
           window.Dispatcher.HasShutdownStarted)return;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            new Action(()=>ClampNow(window)));
    }

    internal static void ClampNow(WidgetShellWindow window)
    {
        if(!window.Dispatcher.CheckAccess())
        {
            window.Dispatcher.BeginInvoke(new Action(()=>ClampNow(window)));
            return;
        }
        if(Moving.Contains(window)||!Assigned.ContainsKey(window)||
           window.WindowState==WindowState.Minimized)return;
        var screen=BoundScreen(window);
        if(screen==null)return;
        var hwnd=new WindowInteropHelper(window).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect)||
           rect.Width<=0||rect.Height<=0)return;

        Moving.Add(window);
        try
        {
            var area=screen.WorkingArea;
            // On small screens, shrink the whole Viewbox at the window level.
            // This preserves the donut's sector hit targets and aspect ratio.
            if(rect.Width>area.Width||rect.Height>area.Height)
            {
                double ratio=Math.Min(1.0,Math.Min(
                    (double)Math.Max(1,area.Width-8)/rect.Width,
                    (double)Math.Max(1,area.Height-8)/rect.Height));
                if(ratio<0.999)
                {
                    double w=double.IsFinite(window.ActualWidth)&&window.ActualWidth>0
                        ?window.ActualWidth:window.Width;
                    double h=double.IsFinite(window.ActualHeight)&&window.ActualHeight>0
                        ?window.ActualHeight:window.Height;
                    if(double.IsFinite(w)&&double.IsFinite(h)&&w>0&&h>0)
                    {
                        window.MinWidth=0;
                        window.MinHeight=0;
                        window.Width=Math.Max(1,w*ratio);
                        window.Height=Math.Max(1,h*ratio);
                        // SizeChanged can arrive while Moving is guarded.
                        // Explicitly enqueue the post-layout native correction.
                        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                            new Action(()=>ClampNow(window)));
                        return;
                    }
                }
            }
            var pos=ClampRectangle(
                new Rectangle(rect.Left,rect.Top,rect.Width,rect.Height),area);
            if(pos.X!=rect.Left||pos.Y!=rect.Top)
                SetWindowPos(hwnd,IntPtr.Zero,pos.X,pos.Y,0,0,
                    NoSize|NoZOrder|NoActivate);
        }
        finally{Moving.Remove(window);}
    }

    // Called AFTER Show() when the window has its own HWND. Avoid multiplying
    // the entire virtual-screen origin by one monitor's DPI conversion.
    internal static void PlaceNewSatellite(WidgetShellWindow window,Forms.Screen screen)
    {
        Bind(window,screen.DeviceName);
        var hwnd=new WindowInteropHelper(window).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect))return;
        var area=screen.WorkingArea;
        int left=area.Right-rect.Width-16;
        int top=area.Top+32;
        var pos=ClampRectangle(new Rectangle(left,top,rect.Width,rect.Height),area);
        if(!SetWindowPos(hwnd,IntPtr.Zero,pos.X,pos.Y,0,0,
            NoSize|NoZOrder|NoActivate))
            System.Diagnostics.Debug.WriteLine("DeskSpace satellite positioning failed.");
        ClampNow(window);
    }
}
