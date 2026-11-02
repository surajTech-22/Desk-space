using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R190: every physical monitor owns its OWN dock Popup, position and scroll
// state. Only the staged-file list is shared. Geometry uses native pixels,
// then converts each widget's coordinates to its own WPF DPI scale.
internal static class DeskSpaceMonitorDockV190
{
    // Per-physical-Deck callbacks. Never share another monitor's visual
    // bounds or scroll position; weak keys allow closed widgets to collect.
    private static readonly ConditionalWeakTable<Border,Action<bool>> ReflowV314=new();
    internal static void RequestReflowV314(Border border,bool resetScroll=false)
    {
        if(ReflowV314.TryGetValue(border,out var reflow))reflow(resetScroll);
    }

    // Measure the CONTENT without the ScrollViewer's previous MaxHeight.
    // The old code measured border.ActualHeight (already clipped), feeding
    // its 30-40px strip back as the NEXT max height forever.
    internal static double DesiredDeckHeightV314(
        FrameworkElement? content,double widthDip,bool minimized)
    {
        if(minimized)return 42;
        if(content==null)return 156;
        content.InvalidateMeasure();
        content.Measure(new System.Windows.Size(
            Math.Max(90,widthDip-16),double.PositiveInfinity));
        var natural=content.DesiredSize.Height+12;
        return Math.Clamp(Math.Max(156,
            double.IsFinite(natural)?natural:156),156,520);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr insertAfter,
        int x,int y,int width,int height,uint flags);
    private const uint NoSize=0x0001,NoZOrder=0x0004,NoActivate=0x0010;

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

    // A saved manual Deck offset and a late WPF HWND clamp can both put
    // the Deck on top of the widget, despite the calculated normal side.
    // Repair the actual popup rectangle against its OWN monitor and leave
    // independent monitor-local offsets intact until explicit dragging.
    internal static Rectangle AvoidWidgetV282(
        Rectangle popup,Rectangle owner,Rectangle work,int gap=12)
    {
        if(!popup.IntersectsWith(Rectangle.Inflate(owner,gap,gap)))
            return popup;
        var safe=Calculate(owner,work,popup.Width,popup.Height,gap);
        if(safe.Side==Side.Contained)return popup;
        return new Rectangle(safe.X,safe.Y,popup.Width,popup.Height);
    }

    // In a tight corner, prefer a smaller SCROLLABLE Deck to a full-height
    // popup painted over the circle. Keep a 12px visual/pointer gap.
    internal static double ScrollHeightV282(Rectangle owner,Rectangle work,
        double dpi,int widthPx,double desiredDip=520)
    {
        if(!double.IsFinite(dpi)||dpi<=0)dpi=1;
        const int gapPixels=14;
        int side=Math.Max(owner.Left-work.Left-gapPixels,
            work.Right-owner.Right-gapPixels);
        if(side>=widthPx)return desiredDip;
        int vertical=Math.Max(owner.Top-work.Top-gapPixels,
            work.Bottom-owner.Bottom-gapPixels);
        // Reserve border size so the total Popup cannot touch the widget.
        return vertical>=44?
            Math.Min(desiredDip,Math.Max(34,(vertical-6)/dpi)):
            desiredDip;
    }


    // R289: the floating Deck always belongs underneath its own clock.
    // If there is no vertical room, move the clock slightly UP on its own
    // monitor, and cap the Deck's scrolling viewport instead of flipping it.
    internal readonly record struct BelowPlanV289(int WidgetTop,int DeckHeightPx);
    internal static BelowPlanV289 PlanBelowV289(
        Rectangle widget,Rectangle work,int desiredPopupPx,int gap=12)
    {
        int usable=Math.Max(1,work.Height-Math.Min(widget.Height,work.Height)-gap-8);
        int deck=Math.Min(Math.Max(1,desiredPopupPx),usable);
        int maxTop=Math.Max(work.Top,work.Bottom-widget.Height-gap-deck-8);
        return new(Math.Clamp(widget.Top,work.Top,maxTop),deck);
    }

    internal static PlacementResult CalculateBelowV289(
        Rectangle widget,Rectangle work,int width,int height,int gap=12)
    {
        int w=Math.Max(1,Math.Min(width,Math.Max(1,work.Width)));
        int h=Math.Max(1,Math.Min(height,Math.Max(1,work.Height)));
        int x=Math.Clamp(widget.Left+(widget.Width-w)/2,
            work.Left,Math.Max(work.Left,work.Right-w));
        return new(x,widget.Bottom+gap,Side.Below,w,h);
    }

    internal static Rectangle RepairBelowV289(
        Rectangle popup,Rectangle widget,Rectangle work,int gap=12)
    {
        int x=Math.Clamp(popup.Left,work.Left,
            Math.Max(work.Left,work.Right-popup.Width));
        // The owner is pre-positioned to make this Y feasible.
        int y=widget.Bottom+gap;
        return new Rectangle(x,y,popup.Width,popup.Height);
    }

    // Trackpad taps shouldn't immediately capture the Deck as a drag.
    // 8 native pixels accommodates precision touchpad cursor jitter.
    internal static bool HasDeckDragIntentV289(int dx,int dy)=>
        // Below-only Deck may move horizontally, not vertically.
        // Vertical touchpad swipes should scroll content, not grab the header.
        Math.Abs(dx)>=8;


    // Every planned Deck is strictly below the circle, with a fixed minimum
    // 12px gap, fully inside the *owning* display's working area.
    // This planner reserves the Deck's full physical HWND height, including
    // border and padding. The older R289 algorithm used a fixed 220px and
    // allowed a taller live HWND to be clamped back OVER the circle.
    internal readonly record struct NativeBelowV292(
        Rectangle Widget,Rectangle Deck,bool Fits);

    internal static NativeBelowV292 FitBelowV292(Rectangle widget,
        Rectangle work,int deckWidth,int requestedDeckHeight,int gap=12)
    {
        if(widget.Width<=0 || widget.Height<=0 || work.Width<=0 || work.Height<=0)
            return new(widget,Rectangle.Empty,false);

        const int edge=6;
        int usable=work.Height-widget.Height-gap-edge*2;
        // An extremely small working area cannot simultaneously show the
        // full clock and an accessible Deck; fail closed (no overlap).
        if(usable<32)return new(widget,Rectangle.Empty,false);

        int height=Math.Clamp(requestedDeckHeight,32,usable);
        int width=Math.Clamp(deckWidth,1,work.Width);
        int x=Math.Clamp(widget.Left+(widget.Width-width)/2,
            work.Left,Math.Max(work.Left,work.Right-width));
        int widgetLeft=Math.Clamp(widget.Left,work.Left,
            Math.Max(work.Left,work.Right-widget.Width));
        int widgetTop=Math.Clamp(widget.Top,
            work.Top+edge,Math.Max(work.Top+edge,
              work.Bottom-edge-widget.Height-gap-height));
        var owner=new Rectangle(widgetLeft,widgetTop,widget.Width,widget.Height);
        var deck=new Rectangle(x,owner.Bottom+gap,width,height);
        return new(owner,deck,work.Contains(deck) &&
            !deck.IntersectsWith(Rectangle.Inflate(owner,0,gap-1)));
    }

    // The popup can be relocated by WPF, Windows or the monitor-fence after
    // the Custom placement callback. Recheck the actual native HWND.
    internal static Rectangle NativeCorrectionV292(Rectangle painted,
        Rectangle owner,Rectangle work,int gap=12)
    {
        var plan=FitBelowV292(owner,work,painted.Width,painted.Height,gap);
        return plan.Fits?plan.Deck:Rectangle.Empty;
    }

    // Called by R153's popup monitor fence during WM_WINDOWPOSCHANGING.
    // Unlike after-paint repairs, this prevents even the FIRST frame of the
    // Running Apps Deck being rendered behind the circular owner.
    internal static Rectangle ConstrainBeforePaintV298(
        Window owner,Rectangle proposed,Rectangle work)
    {
        var hwnd=new WindowInteropHelper(owner).Handle;
        if(hwnd==IntPtr.Zero || proposed.Width<=0 || proposed.Height<=0 ||
            !GetWindowRect(hwnd,out var rect))return proposed;
        var widget=new Rectangle(rect.Left,rect.Top,
            Math.Max(1,rect.Right-rect.Left),Math.Max(1,rect.Bottom-rect.Top));
        var fit=FitBelowV292(widget,work,proposed.Width,proposed.Height);
        if(!fit.Fits)
        {
            // An impossibly small monitor must never paint the Deck over
            // the circle. Its existing post-layout handler closes the
            // popup; until then limit it to a noninteractive 1px edge.
            return new Rectangle(work.Left,work.Top,1,1);
        }
        if(widget.Location!=fit.Widget.Location)
            SetWindowPos(hwnd,IntPtr.Zero,fit.Widget.Left,fit.Widget.Top,
                0,0,NoSize|NoZOrder|NoActivate);

        // Keep horizontal monitor-local manual preference where valid.
        int x=Math.Clamp(proposed.Left,work.Left,
            Math.Max(work.Left,work.Right-fit.Deck.Width));
        return new Rectangle(x,fit.Widget.Bottom+12,
            fit.Deck.Width,fit.Deck.Height);
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

    internal static void Attach(Window owner,Popup popup,Border border,Func<bool>? isMinimizedV314=null)
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
            scroll.CanContentScroll=false;
            scroll.PanningMode=PanningMode.VerticalOnly;
        }
        else
        {
            var contents=border.Child;
            border.Child=null;
            scroll=new ScrollViewer{
                Content=contents,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                CanContentScroll=false,
                PanningMode=PanningMode.VerticalOnly,
                PanningRatio=1.0,
                PanningDeceleration=0.001};
            border.Child=scroll;
        }
        // Recalculate available height and width on this widget's monitor;
        // never use the primary monitor's WPF or pixel scale for satellites.
        void AdjustBounds()
        {
            if(!owner.Dispatcher.CheckAccess())return;
            var source=PresentationSource.FromVisual(owner);
            double sx=DeskSpaceDpiBoundsV195.CleanScale(
                source?.CompositionTarget?.TransformToDevice.M11??1);
            double sy=DeskSpaceDpiBoundsV195.CleanScale(
                source?.CompositionTarget?.TransformToDevice.M22??1);
            var work=Work(owner);
            var widget=Owner(owner,work);
            // Size for the entire live popup, not just a fixed scroll height.
            // A large initial popup must be reduced before WPF can clamp it
            // into the circular clock's physical rectangle.
            // Never measure the clamped border/scroll HWND as the desired
            // content. That created a self-sustaining 40px collapsed strip,
            // with the title and buttons scrolled out of view.
            bool minimized=isMinimizedV314?.Invoke()==true;
            double availableDip=Math.Max(100,Math.Min(310,(work.Width-8)/sx));
            double desiredDip=DesiredDeckHeightV314(
                scroll.Content as FrameworkElement,availableDip,minimized);
            int desired=(int)Math.Ceiling(desiredDip*sy);
            int width=Math.Max(1,Math.Min(work.Width,
                (int)Math.Ceiling(Math.Min(310,work.Width/sx)*sx)));
            var plan=FitBelowV292(widget,work,width,desired);
            border.MaxWidth=Math.Max(1,Math.Min(310,(work.Width-8)/sx));
            border.MinWidth=Math.Min(180,border.MaxWidth);
            if(!plan.Fits)
            {
                // No valid arrangement: do not paint through the circle.
                if(popup.IsOpen)popup.IsOpen=false;
                return;
            }
            // The border includes a 1px outline on each edge; reserve four
            // extra pixels for WPF DPI rounding and native popup margins.
            double max=(plan.Deck.Height-6)/sy;
            // A standard expanded Deck always has a usable viewport, even
            // when only Running Apps currently has data. Explicit manual
            // minimize is still allowed to shrink to the normal header.
            scroll.MinHeight=minimized?0:Math.Min(132,Math.Max(0,max-4));
            border.MinHeight=minimized?0:Math.Min(136,
                Math.Max(0,plan.Deck.Height/sy-6));
            scroll.MaxHeight=Math.Max(20,max);
            border.MaxHeight=Math.Max(22,plan.Deck.Height/sy);
            scroll.InvalidateMeasure();
            border.InvalidateMeasure();
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
        bool dragCandidate=false;
        bool liftingWidget=false;
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
            var saved=DeskSpaceDeckOffsetV228.Read(Device());
            // Legacy saved Y offsets are ignored; below-only is mandatory.
            shift=new DeskSpaceDeckOffsetV228.Offset(saved.X,0);
            positionLoaded=true;
            Reposition();
        }
        bool enforcing=false;
        bool pendingNativeRepair=false;
        void EnsureBelowV289()
        {
            if(enforcing || !owner.Dispatcher.CheckAccess())return;
            var hwnd=new WindowInteropHelper(owner).Handle;
            if(hwnd==IntPtr.Zero)return;
            var work=Work(owner);
            var widget=Owner(owner,work);
            if(widget.Width<=0 || widget.Height<=0)return;
            var source=PresentationSource.FromVisual(owner);
            double sy=DeskSpaceDpiBoundsV195.CleanScale(
                source?.CompositionTarget?.TransformToDevice.M22??1);
            int requested=(int)Math.Ceiling(
                Math.Max(80,border.ActualHeight>1?border.ActualHeight:220)*sy);
            var plan=FitBelowV292(widget,work,
                Math.Max(1,(int)Math.Ceiling(
                    Math.Max(180,border.ActualWidth>1?border.ActualWidth:310))),
                requested);
            if(!plan.Fits)
            {
                if(popup.IsOpen)popup.IsOpen=false;
                return;
            }
            if(plan.Widget.Location==widget.Location)return;
            enforcing=true;
            try
            {
                SetWindowPos(hwnd,IntPtr.Zero,
                    plan.Widget.Left,plan.Widget.Top,
                    0,0,NoSize|NoZOrder|NoActivate);
            }
            finally{enforcing=false;}
        }
        // Explicit reflow on Render: a newly added title/group must expand
        // the Deck even if the native popup is still at its previous size,
        // and there is consequently no SizeChanged event to wake the layout.
        ReflowV314.Remove(border);
        ReflowV314.Add(border,resetScroll=>{
            AdjustBounds();
            if(resetScroll)scroll.ScrollToTop();
            if(popup.IsOpen){Reposition();RepairLater();}
        });
        owner.Closed+=(_,_)=>ReflowV314.Remove(border);
        popup.Opened+=(_,_)=>scroll.ScrollToTop();
        owner.Loaded+=(_,_)=>LoadPosition();
        // Run on opening rather than moving a hidden or manually positioned
        // widget continuously. The shift is limited to this widget's HWND.
        popup.Opened+=(_,_)=>{LoadPosition();EnsureBelowV289();};
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            Popup.IsOpenProperty,typeof(Popup))?.AddValueChanged(popup,
            (_,_)=>{if(popup.IsOpen){EnsureBelowV289();AdjustBounds();Reposition();}});
        border.PreviewMouseLeftButtonDown+=(_,e)=>{
            LoadPosition();
            if(e.GetPosition(border).Y>34)return;
            var child=e.OriginalSource as DependencyObject;
            while(child!=null)
            {
                if(child is Button)return;
                child=VisualTreeHelper.GetParent(child);
            }
            // A light touchpad tap selects normally. Capture only after a
            // definite movement threshold, not on pointer-down.
            dragCandidate=true;
            dragging=false;
            startCursor=Forms.Cursor.Position;
            startOffset=shift;
        };
        border.PreviewMouseMove+=(_,e)=>{
            if(!dragCandidate)return;
            if(e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)
            {dragCandidate=false;return;}
            var now=Forms.Cursor.Position;
            if(!dragging)
            {
                if(!HasDeckDragIntentV289(now.X-startCursor.X,
                    now.Y-startCursor.Y))return;
                dragging=true;
                System.Windows.Input.Mouse.Capture(border);
            }
            shift=DeskSpaceDeckOffsetV228.Clamp(new(
                startOffset.X+now.X-startCursor.X,0));
            Reposition();
            e.Handled=true;
        };
        border.PreviewMouseLeftButtonUp+=(_,e)=>{
            if(!dragCandidate)return;
            dragCandidate=false;
            if(!dragging)return; // Tap: no saved Deck offset.
            dragging=false;
            System.Windows.Input.Mouse.Capture(null);
            DeskSpaceDeckSaveGuardV265.TrySave(() => { DeskSpaceDeckOffsetV228.Save(Device(),shift); });
            e.Handled=true;
        };
        border.LostMouseCapture+=(_,_)=>{
            if(!dragging)return;
            dragging=false;dragCandidate=false;
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
            var widthPx=DeskSpaceDpiBoundsV195.Pixels(
                popupSize.Width,sx,screen.Width);
            var heightPx=DeskSpaceDpiBoundsV195.Pixels(
                popupSize.Height,sy,screen.Height);
            var plan=FitBelowV292(ownerBounds,screen,widthPx,heightPx);
            if(!plan.Fits)
                return Array.Empty<CustomPopupPlacement>();
            // WPF Custom placement is relative to the TARGET top-left,
            // expressed in DIPs, not absolute virtual-desktop pixels.
            // Monitor-local Deck offset stays on the originating display.
            // It is never copied to the second widget's independently
            // persisted Deck position, even with mixed-DPI desktop origins.
            var width=plan.Deck.Width;var height=plan.Deck.Height;
            var deckX=Math.Clamp(plan.Deck.Left+shift.X,screen.Left,
                Math.Max(screen.Left,screen.Right-width));
            var safe=new Rectangle(deckX,
                ownerBounds.Bottom+12,width,height);
            var relative=new System.Windows.Point(
                (safe.Left-ownerBounds.Left)/sx,
                (safe.Top-ownerBounds.Top)/sy);
            return new[]{new CustomPopupPlacement(relative,
                PopupPrimaryAxis.None)};
        };
        void Refresh()
        {
            if(!owner.Dispatcher.CheckAccess() || !owner.IsLoaded)return;
            if(popup.IsOpen)EnsureBelowV289();
            AdjustBounds();
        }
        // Reconcile ACTUAL HWNDs after the popup's separate window is drawn,
        // resized, or clamped by WPF/the monitor fence. The previous callback
        // alone was insufficient; screenshots showed the HWND behind the clock.
        void RepairVisible()
        {
            if(!popup.IsOpen||enforcing)return;
            if(PresentationSource.FromVisual(border) is not HwndSource source ||
               !GetWindowRect(source.Handle,out var actual) ||
               actual.Right<=actual.Left||actual.Bottom<=actual.Top)return;
            var screen=Work(owner);
            var widget=Owner(owner,screen);
            var painted=new Rectangle(actual.Left,actual.Top,
                actual.Right-actual.Left,actual.Bottom-actual.Top);
            var plan=FitBelowV292(widget,screen,painted.Width,painted.Height);
            if(!plan.Fits)
            {
                popup.IsOpen=false;
                return;
            }
            // At the bottom of a screen: make room by moving this widget
            // upward on its OWN display, never by moving the Deck above it.
            if(widget.Location!=plan.Widget.Location)
            {
                var hwnd=new WindowInteropHelper(owner).Handle;
                if(hwnd!=IntPtr.Zero)
                {
                    enforcing=true;
                    try{SetWindowPos(hwnd,IntPtr.Zero,
                        plan.Widget.Left,plan.Widget.Top,
                        0,0,NoSize|NoZOrder|NoActivate);}
                    finally{enforcing=false;}
                }
            }
            var corrected=NativeCorrectionV292(painted,
                Owner(owner,screen),screen);
            if(corrected.IsEmpty){popup.IsOpen=false;return;}
            int actualWidth=Math.Min(corrected.Width,screen.Width);
            int actualHeight=Math.Min(corrected.Height,
                screen.Bottom-corrected.Top);
            if(actualHeight<32){popup.IsOpen=false;return;}
            if(actual.Left==corrected.Left &&
               actual.Top==corrected.Top &&
               actual.Right-actual.Left==actualWidth &&
               actual.Bottom-actual.Top==actualHeight)return;
            SetWindowPos(source.Handle,IntPtr.Zero,
                corrected.Left,corrected.Top,
                actualWidth,actualHeight,NoZOrder|NoActivate);
        }
        void RepairLater()
        {
            if(!owner.Dispatcher.CheckAccess() || !popup.IsOpen ||
               pendingNativeRepair)return;
            pendingNativeRepair=true;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                new Action(()=>{
                    pendingNativeRepair=false;
                    if(popup.IsOpen){AdjustBounds();RepairVisible();}
                }));
        }
        // R295: the Popup is a SEPARATE top-level Win32 window. Explorer,
        // WPF animations, taskbar/DPI changes and R153 monitor fencing can
        // move that HWND AFTER Opened/SizeChanged/LocationChanged. Observe
        // native window position notifications and re-check its actual rect.
        // This is event driven: no background timer, no other monitor touch.
        HwndSource? observedPopupSource=null;
        HwndSourceHook? nativeMoveHook=null;
        const int WindowPosChangedV295=0x0047;
        const int DpiChangedV295=0x02e0;
        void DetachNativeObserverV295()
        {
            if(observedPopupSource!=null && nativeMoveHook!=null &&
                !observedPopupSource.IsDisposed)
                observedPopupSource.RemoveHook(nativeMoveHook);
            observedPopupSource=null;
            nativeMoveHook=null;
        }
        void AttachNativeObserverV295(int retries=0)
        {
            if(!popup.IsOpen)return;
            if(PresentationSource.FromVisual(border) is not HwndSource src)
            {
                if(retries<3)
                    owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(()=>AttachNativeObserverV295(retries+1)));
                return;
            }
            if(ReferenceEquals(observedPopupSource,src))return;
            DetachNativeObserverV295();
            observedPopupSource=src;
            nativeMoveHook=(IntPtr hwnd,int message,IntPtr wparam,
                IntPtr lparam,ref bool handled)=>
            {
                if(message==WindowPosChangedV295 ||
                   message==DpiChangedV295)
                    RepairLater();
                return IntPtr.Zero;
            };
            src.AddHook(nativeMoveHook);
            RepairLater();
        }
        popup.Opened+=(_,_)=>AttachNativeObserverV295();
        popup.Closed+=(_,_)=>DetachNativeObserverV295();
        owner.Closed+=(_,_)=>DetachNativeObserverV295();
        owner.Loaded+=(_,_)=>{Refresh();RepairLater();};
        owner.SizeChanged+=(_,_)=>{Refresh();RepairLater();};
        owner.LocationChanged+=(_,_)=>{Refresh();RepairLater();};
        border.SizeChanged+=(_,_)=>RepairLater();
        popup.Opened+=(_,_)=>{Refresh();RepairLater();};
        // R153 still owns native HWND seam confinement; this additional
        // check only prevents the Deck from covering its own circle.
        Refresh();
    }
}
