using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R152 clamps only WidgetShellWindow HWNDs AFTER Windows has moved them.
// A WPF Popup is a different top-level HWND and may cross the monitor seam.
// R153 binds BOTH the widget and its separate flyout HWNDs to the source screen,
// intercepting proposed moves before Windows paints them.
internal static class DeskSpaceMonitorFenceV153
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public IntPtr hwnd,hwndInsertAfter;
        public int x,y,cx,cy;
        public uint flags;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left,Top,Right,Bottom;
        public int Width=>Right-Left;
        public int Height=>Bottom-Top;
    }
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr insertAfter,
        int x,int y,int width,int height,uint flags);
    private const int WindowPosChanging=0x0046,MovingMessage=0x0216,
        DpiChanged=0x02e0;
    private const uint NoMove=0x0002,NoSize=0x0001,
        NoZOrder=0x0004,NoActivate=0x0010;

    private sealed class Registration
    {
        internal readonly HwndSource Source;
        internal readonly Func<Forms.Screen?> Screen;
        internal readonly HwndSourceHook Hook;
        internal Registration(HwndSource source,Func<Forms.Screen?> screen)
        {
            Source=source;Screen=screen;
            Hook=Handle;
            source.AddHook(Hook);
        }
        private IntPtr Handle(IntPtr hwnd,int message,IntPtr wParam,
            IntPtr lParam,ref bool handled)
        {
            try
            {
                var screen=Screen();
                if(screen==null || lParam==IntPtr.Zero)return IntPtr.Zero;
                var work=screen.WorkingArea;
                if(message==WindowPosChanging)
                {
                    var pos=Marshal.PtrToStructure<WindowPos>(lParam);
                    if(!GetWindowRect(hwnd,out var current))
                        return IntPtr.Zero;
                    var width=(pos.flags&NoSize)!=0?current.Width:pos.cx;
                    var height=(pos.flags&NoSize)!=0?current.Height:pos.cy;
                    var x=(pos.flags&NoMove)!=0?current.Left:pos.x;
                    var y=(pos.flags&NoMove)!=0?current.Top:pos.y;
                    var proposed=new Rectangle(x,y,Math.Max(1,width),Math.Max(1,height));
                    var fit=Constrain(proposed,work);
                    bool changed=false;
                    if(fit.X!=x || fit.Y!=y)
                    {
                        pos.flags&=~NoMove;
                        pos.x=fit.X;pos.y=fit.Y;
                        changed=true;
                    }
                    if(fit.Width!=width || fit.Height!=height)
                    {
                        pos.flags&=~NoSize;
                        pos.cx=fit.Width;pos.cy=fit.Height;
                        changed=true;
                    }
                    if(changed)Marshal.StructureToPtr(pos,lParam,false);
                    // We modify Windows' input struct, not handled: other
                    // WPF hooks still see the proposed message.
                }
                else if(message==MovingMessage || message==DpiChanged)
                {
                    var rect=Marshal.PtrToStructure<NativeRect>(lParam);
                    if(rect.Width<=0||rect.Height<=0)return IntPtr.Zero;
                    var fit=Constrain(new Rectangle(rect.Left,rect.Top,
                        rect.Width,rect.Height),work);
                    rect.Left=fit.Left;rect.Top=fit.Top;
                    rect.Right=fit.Right;rect.Bottom=fit.Bottom;
                    Marshal.StructureToPtr(rect,lParam,false);
                }
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine(
                "DeskSpace per-screen HWND positioning: "+ex);}
            return IntPtr.Zero;
        }
        internal void Uninstall()
        {
            if(!Source.IsDisposed)Source.RemoveHook(Hook);
        }
    }

    // Pure pixel geometry for tests, including negative-origin secondary
    // monitors, expanded donuts, and different-sized display work areas.
    internal static Rectangle Constrain(Rectangle desired,Rectangle work)
    {
        var width=Math.Max(1,Math.Min(desired.Width,Math.Max(1,work.Width)));
        var height=Math.Max(1,Math.Min(desired.Height,Math.Max(1,work.Height)));
        var x=Math.Clamp(desired.Left,work.Left,Math.Max(work.Left,work.Right-width));
        var y=Math.Clamp(desired.Top,work.Top,Math.Max(work.Top,work.Bottom-height));
        return new Rectangle(x,y,width,height);
    }

    private static readonly Dictionary<Window,Registration> Windows=new();
    private static readonly Dictionary<Popup,Registration> Popups=new();
    private static readonly Dictionary<Popup,Forms.Screen> PopupScreen=new();

    internal static void TrackWidget(WidgetShellWindow widget)
    {
        if(Windows.ContainsKey(widget))return;
        void Install(object? _,EventArgs __)
        {
            if(Windows.ContainsKey(widget))return;
            var source=PresentationSource.FromVisual(widget) as HwndSource;
            if(source==null)return;
            var registration=new Registration(source,()=>
                DeskSpaceMonitorFenceV152.BoundScreen(widget));
            Windows[widget]=registration;
            ClampNow(source,registration.Screen);
        }
        widget.SourceInitialized+=Install;
        widget.Closed+=(_,_)=>{
            if(Windows.Remove(widget,out var link))link.Uninstall();
        };
        if(new WindowInteropHelper(widget).Handle!=IntPtr.Zero)Install(widget,EventArgs.Empty);
    }

    // owner can be the main widget or a visual inside another Popup. The
    // anchor is sampled before WPF can flip the newly opened popup to a
    // neighbouring screen; nested popups inherit the parent popup's screen.
    internal static void TrackPopup(FrameworkElement anchor,Popup popup)
    {
        if(PopupScreen.ContainsKey(popup))return;
        Forms.Screen ScreenForAnchor()
        {
            var window=Window.GetWindow(anchor) as WidgetShellWindow;
            if(window!=null)
                return DeskSpaceMonitorFenceV152.BoundScreen(window) ??
                    Forms.Screen.PrimaryScreen!;
            if(PresentationSource.FromVisual(anchor) is HwndSource source)
            {
                foreach(var linked in Popups)
                    if(ReferenceEquals(linked.Value.Source,source)&&
                       PopupScreen.TryGetValue(linked.Key,out var known))
                        return known;
            }
            try
            {
                var point=anchor.PointToScreen(new System.Windows.Point(1,1));
                return Forms.Screen.FromPoint(new System.Drawing.Point(
                    (int)Math.Round(point.X),(int)Math.Round(point.Y)));
            }
            catch(InvalidOperationException)
            {
                return Forms.Screen.PrimaryScreen ??
                    Forms.Screen.AllScreens[0];
            }
        }
        PopupScreen[popup]=ScreenForAnchor();
        void Opened(object? _,EventArgs __)
        {
            if(!popup.IsOpen||popup.Child==null)return;
            if(PresentationSource.FromVisual(popup.Child) is not HwndSource source)
            {
                popup.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                    new Action(()=>Opened(popup,EventArgs.Empty)));
                return;
            }
            if(Popups.TryGetValue(popup,out var existing))
            {
                if(ReferenceEquals(existing.Source,source))
                {
                    ClampNow(source,existing.Screen);return;
                }
                existing.Uninstall();
                Popups.Remove(popup);
            }
            Func<Forms.Screen?> assigned=()=>
                PopupScreen.TryGetValue(popup,out var screen)?screen:null;
            var registration=new Registration(source,assigned);
            Popups[popup]=registration;
            ClampNow(source,assigned);
        }
        popup.Opened+=Opened;
        popup.Closed+=(_,_)=>{
            if(Popups.Remove(popup,out var registration))
                registration.Uninstall();
        };
        if(popup.IsOpen)Opened(popup,EventArgs.Empty);
    }

    private static void ClampNow(HwndSource source,Func<Forms.Screen?> monitor)
    {
        if(source.IsDisposed)return;
        var screen=monitor();
        if(screen==null||!GetWindowRect(source.Handle,out var native))return;
        if(native.Width<=0||native.Height<=0)return;
        var fit=Constrain(new Rectangle(native.Left,native.Top,native.Width,
            native.Height),screen.WorkingArea);
        if(fit.X!=native.Left||fit.Y!=native.Top||
           fit.Width!=native.Width||fit.Height!=native.Height)
            SetWindowPos(source.Handle,IntPtr.Zero,fit.X,fit.Y,fit.Width,fit.Height,
                NoZOrder|NoActivate);
    }

    // Called by WPF smoke tests on a real HWND, not a mocked rectangle.
    internal static Rectangle? PopupBounds(Popup popup)
    {
        if(popup.Child==null||
           PresentationSource.FromVisual(popup.Child) is not HwndSource source||
           !GetWindowRect(source.Handle,out var rect))return null;
        return new Rectangle(rect.Left,rect.Top,rect.Width,rect.Height);
    }
}
