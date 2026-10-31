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
        // Existing Deck generations use different content containers:
        // some place StackPanel directly in Border, newer versions already
        // wrap or group it. Preserve that FULL subtree and any event handlers.
        if(border.Child is null)
            throw new InvalidOperationException("R190 cannot attach to an empty Deck");
        ScrollViewer scroll;
        if(border.Child is ScrollViewer existingScroll)
        {
            scroll=existingScroll;
            scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
        }
        else
        {
            var contents=border.Child;
            border.Child=null;
            scroll=new ScrollViewer{
                Content=contents,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                CanContentScroll=true};
            border.Child=scroll;
        }
        // Recalculate available height and width on this widget's monitor;
        // never use the primary monitor's WPF or pixel scale for satellites.
        void AdjustBounds()
        {
            if(!owner.Dispatcher.CheckAccess())return;
            var source=PresentationSource.FromVisual(owner);
            double sx=source?.CompositionTarget?.TransformToDevice.M11??1;
            double sy=source?.CompositionTarget?.TransformToDevice.M22??1;
            sx=DeskSpaceDpiBoundsV195.CleanScale(sx);
            sy=DeskSpaceDpiBoundsV195.CleanScale(sy);
            var bounds=Work(owner);
            // Reserve a little for the dock Border, Windows work-area edges,
            // and current screen-DPI rounding.
            double w=Math.Max(1,(bounds.Width-16)/sx);
            double h=Math.Max(1,(bounds.Height-28)/sy);
            border.MaxWidth=Math.Min(310,w);
            border.MinWidth=Math.Min(180,border.MaxWidth);
            scroll.MaxHeight=Math.Min(520,h);
        }
        // Each Deck owns its position and drag state independently. Only
        // staged held-file DATA is shared with the other physical display.
        string Device()
        {
            var widget=owner as WidgetShellWindow;
            return widget==null?
                Forms.Screen.FromHandle(new WindowInteropHelper(owner).Handle).DeviceName:
                DeskSpaceMonitorSessionsV154.Device(widget) ??
                DeskSpaceMonitorFenceV152.BoundScreen(widget)?.DeviceName ??
                Forms.Screen.FromHandle(new WindowInteropHelper(owner).Handle).DeviceName;
        }
        // At constructor time a satellite has not yet been attached to its
        // native monitor session. Reading here silently copied monitor one's
        // Deck offset into BOTH widgets.
        DeskSpaceDeckOffsetV228.Offset shift=new(0,0);
        bool positionLoaded=false;
        bool dragging=false;
        System.Drawing.Point startCursor=default;
        DeskSpaceDeckOffsetV228.Offset startOffset=new(0,0);
        void Reposition()
        {
            // A tiny alternating horizontal offset requests WPF's Custom
            // placement callback again, without relocating the OTHER Deck.
            popup.HorizontalOffset=popup.HorizontalOffset==0?0.001:0;
        }
        void LoadPosition()
        {
            if(positionLoaded)return;
            // Do not guess the primary display before a satellite has its
            // permanent physical screen assignment.
            if(owner is WidgetShellWindow widget &&
                DeskSpaceMonitorSessionsV154.Device(widget)==null)return;
            shift=DeskSpaceDeckOffsetV228.Read(Device());
            positionLoaded=true;
            Reposition();
        }
        owner.Loaded+=(_,_)=>LoadPosition();
        popup.Opened+=(_,_)=>LoadPosition();
        border.PreviewMouseLeftButtonDown+=(_,e)=>{
            LoadPosition();
            if(e.GetPosition(border).Y>34)return;
            var child=e.OriginalSource as DependencyObject;
            while(child!=null)
            {
                if(child is Button)return;
                child=VisualTreeHelper.GetParent(child);
            }
            dragging=true;
            startCursor=Forms.Cursor.Position;
            startOffset=shift;
            System.Windows.Input.Mouse.Capture(border);
            e.Handled=true;
        };
        border.PreviewMouseMove+=(_,e)=>{
            if(!dragging)return;
            if(e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)return;
            var now=Forms.Cursor.Position;
            shift=DeskSpaceDeckOffsetV228.Clamp(new(
                startOffset.X+now.X-startCursor.X,
                startOffset.Y+now.Y-startCursor.Y));
            Reposition();
            e.Handled=true;
        };
        border.PreviewMouseLeftButtonUp+=(_,e)=>{
            if(!dragging)return;
            dragging=false;
            System.Windows.Input.Mouse.Capture(null);
            DeskSpaceDeckSaveGuardV265.TrySave(() => { DeskSpaceDeckOffsetV228.Save(Device(),shift); });
            e.Handled=true;
        };
        border.LostMouseCapture+=(_,_)=>{
            if(!dragging)return;
            dragging=false;
            DeskSpaceDeckSaveGuardV265.TrySave(() => { DeskSpaceDeckOffsetV228.Save(Device(),shift); });
        };
        // R234: recovery control belongs to THIS popup Border and THIS
        // physical display. A user can fix an inconvenient Deck offset
        // without clearing shared held files, modifying the other Deck
        // or resetting monitor-specific widget positions.
        var resetDeckThisMonitor=new MenuItem
        {
            Header="Reset Deck position on this monitor",
            ToolTip="Reset only this display's Deck location; keep all held files"
        };
        resetDeckThisMonitor.Click+=(_,_)=>{
            LoadPosition();
            shift=new DeskSpaceDeckOffsetV228.Offset(0,0);
            positionLoaded=true;
            DeskSpaceDeckSaveGuardV265.TrySave(() => { DeskSpaceDeckOffsetV228.Save(Device(),shift); });
            Reposition();
        };
        var deckContext=border.ContextMenu ?? new ContextMenu();
        deckContext.Items.Add(resetDeckThisMonitor);
        border.ContextMenu=deckContext;
        popup.PlacementTarget=owner;
        popup.Placement=PlacementMode.Custom;
        popup.CustomPopupPlacementCallback=(popupSize,targetSize,offset)=>{
            var screen=Work(owner);
            var source=PresentationSource.FromVisual(owner);
            double sx=source?.CompositionTarget?.TransformToDevice.M11??1;
            double sy=source?.CompositionTarget?.TransformToDevice.M22??1;
            sx=DeskSpaceDpiBoundsV195.CleanScale(sx);
            sy=DeskSpaceDpiBoundsV195.CleanScale(sy);
            var ownerBounds=Owner(owner,screen);
            var result=Calculate(ownerBounds,screen,
                DeskSpaceDpiBoundsV195.Pixels(popupSize.Width,sx,screen.Width),
                DeskSpaceDpiBoundsV195.Pixels(popupSize.Height,sy,screen.Height));
            // WPF Custom placement is relative to the TARGET top-left,
            // expressed in DIPs, not absolute virtual-desktop pixels.
            // Monitor-local Deck offset stays on the originating display.
            // It is never copied to the second widget's independently
            // persisted Deck position, even with mixed-DPI desktop origins.
            var width=result.Width;var height=result.Height;
            var deckX=Math.Clamp(result.X+shift.X,screen.Left,
                Math.Max(screen.Left,screen.Right-width));
            var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,
                Math.Max(screen.Top,screen.Bottom-height));
            var relative=new System.Windows.Point(
                (deckX-ownerBounds.Left)/sx,
                (deckY-ownerBounds.Top)/sy);
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
