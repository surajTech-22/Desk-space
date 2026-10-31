using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Keep an explicitly opened pinned-document picker on the monitor of the
// widget that opened it. All Win32 rectangles are PHYSICAL screen pixels.
// WPF Left/Top and SystemParameters.WorkArea cannot be mixed with them.
internal static class DeskSpacePinnedRecentMonitorV264
{
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
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,
        int x,int y,int width,int height,uint flags);
    private const uint NoSize=0x0001,NoZOrder=0x0004,NoActivate=0x0010;

    internal static Point CentreOnOwner(Rectangle popup,Rectangle owner,
        Rectangle work)
    {
        var desired=new Rectangle(
            owner.Left+(owner.Width-popup.Width)/2,
            owner.Top+(owner.Height-popup.Height)/2,
            popup.Width,popup.Height);
        return DeskSpaceMonitorFenceV152.ClampRectangle(desired,work);
    }

    internal static Point ClampToMonitor(Rectangle popup,Rectangle work)=>
        DeskSpaceMonitorFenceV152.ClampRectangle(popup,work);

    internal static void Attach(Window dialog,Window owner)
    {
        bool moving=false;
        bool initiallyPlaced=false;
        void Fence(bool initial)
        {
            if(moving || dialog.Dispatcher.HasShutdownStarted ||
               dialog.WindowState==WindowState.Minimized)return;
            var hwnd=new WindowInteropHelper(dialog).Handle;
            var ownerHwnd=new WindowInteropHelper(owner).Handle;
            if(hwnd==IntPtr.Zero || ownerHwnd==IntPtr.Zero ||
               !GetWindowRect(hwnd,out var popup) ||
               popup.Width<=0 || popup.Height<=0)return;
            var screen=owner is WidgetShellWindow widget?
                DeskSpaceMonitorFenceV152.BoundScreen(widget):
                Forms.Screen.FromHandle(ownerHwnd);
            if(screen==null)return;
            var work=screen.WorkingArea;

            moving=true;
            try
            {
                // A small secondary display needs the scrollable chooser
                // to shrink, never to spill onto the neighbouring desktop.
                var source=HwndSource.FromHwnd(hwnd);
                var dip=source?.CompositionTarget?.TransformFromDevice;
                double scaleX=dip?.M11??1,scaleY=dip?.M22??1;
                double maxWidth=Math.Max(1,(work.Width-12)*scaleX);
                double maxHeight=Math.Max(1,(work.Height-12)*scaleY);
                bool resize=false;
                if(popup.Width>work.Width-12 &&
                   double.IsFinite(maxWidth) && maxWidth>1)
                {
                    dialog.MinWidth=Math.Min(dialog.MinWidth,maxWidth);
                    dialog.Width=Math.Min(dialog.Width,maxWidth);
                    resize=true;
                }
                if(popup.Height>work.Height-12 &&
                   double.IsFinite(maxHeight) && maxHeight>1)
                {
                    dialog.MinHeight=Math.Min(dialog.MinHeight,maxHeight);
                    dialog.Height=Math.Min(dialog.Height,maxHeight);
                    resize=true;
                }
                // WPF recomputes the native HWND rectangle on SizeChanged.
                // Re-center only once, after layout has accepted the size.
                if(resize)return;

                var bounds=new Rectangle(popup.Left,popup.Top,
                    popup.Width,popup.Height);
                var ownerBounds=GetWindowRect(ownerHwnd,out var o)?
                    new Rectangle(o.Left,o.Top,o.Width,o.Height):
                    screen.WorkingArea;
                var position=initial && !initiallyPlaced?
                    CentreOnOwner(bounds,ownerBounds,work):
                    ClampToMonitor(bounds,work);
                if(popup.Left!=position.X || popup.Top!=position.Y)
                    SetWindowPos(hwnd,IntPtr.Zero,position.X,position.Y,
                        0,0,NoSize|NoZOrder|NoActivate);
                if(initial)initiallyPlaced=true;
            }
            finally{moving=false;}
        }
        dialog.SourceInitialized+=(_,_)=>Fence(true);
        dialog.ContentRendered+=(_,_)=>Fence(true);
        dialog.SizeChanged+=(_,_)=>Fence(!initiallyPlaced);
        dialog.LocationChanged+=(_,_)=>{
            if(initiallyPlaced)Fence(false);
        };
    }
}
