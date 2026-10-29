using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R190: every physical monitor owns its OWN dock Popup, position and scroll
// state. Only the staged-file list is shared. Geometry uses native pixels,
// then converts each widget's coordinates to its own WPF DPI scale.
internal static class DeskSpaceMonitorDockV190
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);

    internal enum Side { Below,Above,Right,Left,Contained }
    internal readonly record struct PlacementResult(
        int X,int Y,Side Side,int Width,int Height);

    // The placement operation is independent of virtual-desktop origin:
    // negative X/Y and dissimilar monitor resolutions are first-class.
    internal static PlacementResult Calculate(Rectangle owner,Rectangle work,
        int width,int height,int gap=6)
    {
        int w=Math.Max(1,Math.Min(width,Math.Max(1,work.Width)));
        int h=Math.Max(1,Math.Min(height,Math.Max(1,work.Height)));
        int xCenter=owner.Left+(owner.Width-w)/2;
        int yCenter=owner.Top+(owner.Height-h)/2;
        int ClampX(int x)=>Math.Clamp(x,work.Left,Math.Max(work.Left,work.Right-w));
        int ClampY(int y)=>Math.Clamp(y,work.Top,Math.Max(work.Top,work.Bottom-h));
        if(owner.Bottom+gap+h<=work.Bottom)
            return new(ClampX(xCenter),owner.Bottom+gap,Side.Below,w,h);
        if(owner.Top-gap-h>=work.Top)
            return new(ClampX(xCenter),owner.Top-gap-h,Side.Above,w,h);
        if(owner.Right+gap+w<=work.Right)
            return new(owner.Right+gap,ClampY(yCenter),Side.Right,w,h);
        if(owner.Left-gap-w>=work.Left)
            return new(owner.Left-gap-w,ClampY(yCenter),Side.Left,w,h);
        // On compact displays a fully separate popup may not fit on ANY side.
        // Keep it wholly within this monitor and let its own scroll viewer
        // expose all items; never rely on Windows shifting it to a neighbour.
        int freeBelow=Math.Max(0,work.Bottom-owner.Bottom);
        int freeAbove=Math.Max(0,owner.Top-work.Top);
        int y=freeBelow>=freeAbove?owner.Bottom+gap:owner.Top-gap-h;
        return new(ClampX(xCenter),ClampY(y),Side.Contained,w,h);
    }

    private static Rectangle Work(Window window)
    {
        if(window is WidgetShellWindow widget)
        {
            var assigned=DeskSpaceMonitorFenceV152.BoundScreen(widget);
            if(assigned!=null)return assigned.WorkingArea;
        }
        var hwnd=new WindowInteropHelper(window).Handle;
        return (hwnd!=IntPtr.Zero
            ?Forms.Screen.FromHandle(hwnd)
            :Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0]).WorkingArea;
    }

    private static Rectangle Owner(Window window,Rectangle fallback)
    {
        var hwnd=new WindowInteropHelper(window).Handle;
        if(hwnd!=IntPtr.Zero && GetWindowRect(hwnd,out var rect) &&
            rect.Right>rect.Left && rect.Bottom>rect.Top)
            return new(rect.Left,rect.Top,
                rect.Right-rect.Left,rect.Bottom-rect.Top);
        return fallback;
    }

    internal static void Attach(Window owner,Popup popup,Border border)
    {
        if(border.Child is not StackPanel contents)
            throw new InvalidOperationException("R190 requires the existing dock list");
        border.Child=null;
        var scroll=new ScrollViewer{
            Content=contents,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            CanContentScroll=true};
        border.Child=scroll;
        // Recalculate available height and width on this widget's monitor;
        // never use the primary monitor's WPF or pixel scale for satellites.
        void AdjustBounds()
        {
            if(!owner.Dispatcher.CheckAccess())return;
            var source=PresentationSource.FromVisual(owner);
            double sx=source?.CompositionTarget?.TransformToDevice.M11??1;
            double sy=source?.CompositionTarget?.TransformToDevice.M22??1;
            if(!double.IsFinite(sx)||sx<=0)sx=1;
            if(!double.IsFinite(sy)||sy<=0)sy=1;
            var bounds=Work(owner);
            // Reserve a little for the dock Border, Windows work-area edges,
            // and current screen-DPI rounding.
            double w=Math.Max(1,(bounds.Width-16)/sx);
            double h=Math.Max(1,(bounds.Height-28)/sy);
            border.MaxWidth=Math.Min(310,w);
            border.MinWidth=Math.Min(180,border.MaxWidth);
            scroll.MaxHeight=Math.Min(520,h);
        }
        popup.PlacementTarget=owner;
        popup.Placement=PlacementMode.Custom;
        popup.CustomPopupPlacementCallback=(popupSize,targetSize,offset)=>{
            var screen=Work(owner);
            var source=PresentationSource.FromVisual(owner);
            double sx=source?.CompositionTarget?.TransformToDevice.M11??1;
            double sy=source?.CompositionTarget?.TransformToDevice.M22??1;
            if(!double.IsFinite(sx)||sx<=0)sx=1;
            if(!double.IsFinite(sy)||sy<=0)sy=1;
            var ownerBounds=Owner(owner,screen);
            var result=Calculate(ownerBounds,screen,
                (int)Math.Ceiling(popupSize.Width*sx),
                (int)Math.Ceiling(popupSize.Height*sy));
            // WPF Custom placement is relative to the TARGET top-left,
            // expressed in DIPs, not absolute virtual-desktop pixels.
            var relative=new System.Windows.Point(
                (result.X-ownerBounds.Left)/sx,
                (result.Y-ownerBounds.Top)/sy);
            return new[]{new CustomPopupPlacement(relative,
                PopupPrimaryAxis.None)};
        };
        void Refresh()
        {
            if(!owner.Dispatcher.CheckAccess() || !owner.IsLoaded)return;
            AdjustBounds();
        }
        owner.Loaded+=(_,_)=>Refresh();
        owner.SizeChanged+=(_,_)=>Refresh();
        popup.Opened+=(_,_)=>Refresh();
        // R153 already owns synchronous HWND clamping. Keep R190's
        // independent WPF placement and R153 Win32 fence in agreement.
        Refresh();
    }
}
