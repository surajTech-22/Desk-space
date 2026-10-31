using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Popups live OUTSIDE the main deck StackPanel. Hover cannot resize
// or reposition the deck, even when an app has dozens of windows.
internal static class DeskSpaceStableHoverV93
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X,Y; }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    // A removed row can disappear under a motionless mouse. That is NOT
    // equivalent to the user leaving the flyout. Observe real pointer
    // movement before dismissing a panel after a close request.
    internal static bool ShouldRetainAfterClose(int originalX,int originalY,
        int currentX,int currentY)=>
        Math.Abs(currentX-originalX)<=12 &&
        Math.Abs(currentY-originalY)<=12;

    private static Popup? activeIconPopup;
    internal static bool IsHoverActive=>activeIconPopup?.IsOpen==true;

    // Pure filter shared by the UI timer and Windows smoke tests.
    // Do not remove windows merely because WM_CLOSE was requested:
    // the app may be displaying an unsaved-work confirmation.
    internal static IReadOnlyList<DeskSpaceRunningAppsV88.Entry> RemainingWindows(
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> windows,
        Func<IntPtr,bool> isAlive)=>
        windows.Where(x=>isAlive(x.Handle)).ToArray();

    // Required space checks are deliberately pure, so the side selection
    // is tested without relying on an active desktop session.
    internal static bool PreferRight(double roomLeft,double roomRight,
        double iconFraction,double neededWidth)
    {
        if(roomLeft>=neededWidth && roomRight>=neededWidth)
            return iconFraction>=0.5;
        if(roomRight>=neededWidth)return true;
        if(roomLeft>=neededWidth)return false;
        return roomRight>=roomLeft;
    }

    // Fix the side menu outside its icon panel. Custom placement prevents
    // WPF's Right/Left automatic placement fallback from putting the list
    // OVER the center of the deck or its app tiles at screen edges.
    internal static double WindowFlyoutX(bool right,double targetWidth,
        double popupWidth,double gap=4)=>
        right?targetWidth+gap:-popupWidth-gap;

    // Never allow WPF to bring an app's multi-window flyout into the
    // application grid or across the middle of the floating deck.
    // Available room is in PHYSICAL monitor pixels; popup widths in DIPs.
    internal readonly record struct WindowSideResult(
        bool CanOpen,bool Right,double WidthDip);
    internal static WindowSideResult ChooseWindowSide(
        double freeLeftPx,double freeRightPx,double dpi,bool outwardRight)
    {
        if(!double.IsFinite(dpi) || dpi<=0)dpi=1;
        // Reserve space for monitor edges, border and DPI rounding.
        double l=Math.Max(0,freeLeftPx/dpi-14);
        double r=Math.Max(0,freeRightPx/dpi-14);
        bool leftFits=l>=128,rightFits=r>=128;
        if(!leftFits && !rightFits)
            return new(false,false,0);
        bool right=leftFits && rightFits?outwardRight:
            rightFits && !leftFits?true:
            leftFits && !rightFits?false:
            r>=l;
        double width=Math.Min(276,right?r:l);
        return new(true,right,width);
    }

    // Four independent orientations, always OUTSIDE the floating app grid.
    // Work-area distances arrive in physical pixels; all WPF placement uses DIPs.
    internal enum WindowDirectionV115 { Left, Right, Up, Down }
    internal readonly record struct WindowPlacementV115(
        bool CanOpen,WindowDirectionV115 Direction,
        double WidthDip,double MaxHeightDip);

    internal static WindowPlacementV115 ChooseFourWayPlacement(
        double freeLeftPx,double freeRightPx,double freeTopPx,
        double freeBottomPx,double monitorWidthPx,double dpi,
        int windowCount,bool outwardRight)
    {
        if(!double.IsFinite(dpi) || dpi<=0)dpi=1;
        double left=Math.Max(0,freeLeftPx/dpi-12);
        double right=Math.Max(0,freeRightPx/dpi-12);
        double above=Math.Max(0,freeTopPx/dpi-12);
        double below=Math.Max(0,freeBottomPx/dpi-12);
        double screenWidth=Math.Max(0,monitorWidthPx/dpi-24);
        double desiredHeight=Math.Min(292,Math.Max(100,34+Math.Max(1,windowCount)*30));
        bool leftOK=left>=128,rightOK=right>=128;
        bool upOK=above>=96 && screenWidth>=128;
        bool downOK=below>=96 && screenWidth>=128;
        bool preferRight=outwardRight;
        WindowDirectionV115 side=preferRight?WindowDirectionV115.Right:WindowDirectionV115.Left;
        double sideRoom=preferRight?right:left;
        if(sideRoom<128 && (leftOK || rightOK))
        {
            side=leftOK?WindowDirectionV115.Left:WindowDirectionV115.Right;
            sideRoom=leftOK?left:right;
        }
        if(leftOK && rightOK && sideRoom<260)
        {
            if((preferRight?left:right)>sideRoom+64)
            {
                side=preferRight?WindowDirectionV115.Left:WindowDirectionV115.Right;
                sideRoom=Math.Max(left,right);
            }
        }
        // Prefer a full-width lateral list, preserving established behaviour.
        if(sideRoom>=260)
            return new(true,side,Math.Min(276,sideRoom),292);
        // If the side would squeeze window titles, open the complete list
        // above/below the app grid instead. Prefer more free vertical space.
        bool verticalFits=(upOK && above>=desiredHeight) ||
                          (downOK && below>=desiredHeight);
        if(verticalFits)
        {
            bool belowWins=downOK && below>=desiredHeight &&
                (!upOK || above<desiredHeight || below>=above);
            return new(true,belowWins?WindowDirectionV115.Down:
                WindowDirectionV115.Up,
                Math.Min(276,screenWidth),292);
        }
        // No direction fits the full list: scroll in the largest viable area.
        if(sideRoom>=128 && (sideRoom>=Math.Max(above,below)*0.8))
            return new(true,side,Math.Min(276,sideRoom),292);
        if(upOK || downOK)
        {
            bool belowWins=downOK && (!upOK || below>=above);
            double available=belowWins?below:above;
            return new(true,belowWins?WindowDirectionV115.Down:
                WindowDirectionV115.Up,
                Math.Min(276,screenWidth),Math.Min(292,available));
        }
        if(sideRoom>=128)
            return new(true,side,Math.Min(276,sideRoom),292);
        return new(false,WindowDirectionV115.Left,0,0);
    }

    internal static Point WindowFlyoutFourWay(
        WindowDirectionV115 direction,double targetWidth,double targetHeight,
        double popupWidth,double popupHeight,double rowTop,double alignX,
        double workLeft,double workRight,double workTop,double workBottom)
    {
        static double Clamp(double value,double low,double high)=>
            Math.Clamp(value,low,Math.Max(low,high));
        double y=Clamp(rowTop,workTop,workBottom-popupHeight);
        double x=Clamp(alignX-popupWidth/2,workLeft,workRight-popupWidth);
        return direction switch
        {
            WindowDirectionV115.Left=>new Point(-popupWidth-6,y),
            WindowDirectionV115.Right=>new Point(targetWidth+6,y),
            WindowDirectionV115.Up=>new Point(x,-popupHeight-6),
            _=>new Point(x,targetHeight+6)
        };
    }

    // Multi-window applications sit on the OUTSIDE edges. If every app has
    // exactly one window, alphabetical ordering applies throughout.
    // More windows take precedence; subsequent grouped apps alternate edges.
    // Place groups on actual outside columns of a THREE-column grid.
    // A flat leading/trailing sort is wrong whenever a row wraps.
    internal static string[] OrderAppNamesV115(
        IEnumerable<(string Name,int Count)> groups,bool outwardRight)
    {
        var items=groups.ToArray();
        var all=items.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var multi=all.Where(x=>x.Count>1)
            .OrderByDescending(x=>x.Count)
            .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        var single=all.Where(x=>x.Count<=1)
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        if(multi.Length==0)return single.Select(x=>x.Name).ToArray();

        int n=all.Length;
        string?[] grid=new string?[n];
        // Edge positions 0,2,3,5,6,8...; put largest group on
        // the outward side first, then distribute other groups.
        var edgeSlots=Enumerable.Range(0,n)
            .Where(i=>i%3==0 || i%3==2)
            .OrderBy(i=>i/3)
            .ThenBy(i=>outwardRight?(i%3==2?0:1):
                (i%3==0?0:1)).ToArray();
        var fallbackSlots=Enumerable.Range(0,n)
            .Where(i=>i%3==1).ToArray();
        int multiIndex=0;
        foreach(var item in multi)
        {
            int slot=multiIndex<edgeSlots.Length?
                edgeSlots[multiIndex]:
                fallbackSlots[multiIndex-edgeSlots.Length];
            grid[slot]=item.Name;
            multiIndex++;
        }
        int singleIndex=0;
        for(int i=0;i<n;i++)
            if(grid[i]==null)grid[i]=single[singleIndex++].Name;
        return grid.Select(x=>x!).ToArray();
    }

    // Shared per-window close control. Separate from the switch button, so
    // clicking X never activates or minimises a window.
    // Real two-dimensional layout: grouped multi-window apps occupy the
    // outside columns, and remaining one-window apps fill central slots.
    // Only if ALL apps have one window do we use simple alphabetical rows.
    internal readonly record struct AppGridSlotV117(
        string Name,int Row,int Column);

    internal static AppGridSlotV117[] PlanAppGridV117(
        IEnumerable<(string Name,int Count)> groups,bool outwardRight)
    {
        var all=groups.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if(all.Length==0)return Array.Empty<AppGridSlotV117>();
        int rows=(all.Length+2)/3;
        var multi=all.Where(x=>x.Count>1)
            .OrderByDescending(x=>x.Count)
            .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        var single=all.Where(x=>x.Count<=1).ToArray();
        var placements=new List<AppGridSlotV117>(all.Length);
        if(multi.Length==0)
        {
            for(int i=0;i<single.Length;i++)
                placements.Add(new(single[i].Name,i/3,i%3));
            return placements.ToArray();
        }
        var occupied=new HashSet<(int Row,int Column)>();
        var edgeSlots=new List<(int Row,int Column)>();
        for(int r=0;r<rows;r++)
        {
            edgeSlots.Add((r,outwardRight?2:0));
            edgeSlots.Add((r,outwardRight?0:2));
        }
        for(int i=0;i<multi.Length;i++)
        {
            var slot=i<edgeSlots.Count?edgeSlots[i]:
                (Row:i-edgeSlots.Count,Column:1);
            occupied.Add(slot);
            placements.Add(new(multi[i].Name,slot.Row,slot.Column));
        }
        var middle=Enumerable.Range(0,rows).Select(r=>(Row:r,Column:1))
            .Where(slot=>!occupied.Contains(slot));
        var edges=Enumerable.Range(0,rows)
            .SelectMany(r=>new[]{(Row:r,Column:0),(Row:r,Column:2)})
            .Where(slot=>!occupied.Contains(slot));
        var emptySlots=middle.Concat(edges).ToArray();
        if(emptySlots.Length<single.Length)
            throw new InvalidOperationException("Running Apps grid capacity mismatch.");
        for(int i=0;i<single.Length;i++)
            placements.Add(new(single[i].Name,emptySlots[i].Row,
                emptySlots[i].Column));
        return placements.ToArray();
    }
    // R118: "left of the whole grid" is NOT reachable from an Excel tile
    // at the RIGHT edge when the floating deck occupies the outward side.
    // Prefer a flyout immediately above/below the *whole* grid, aligned
    // to the hovered tile. Only fall back to the opposite side when there
    // is no reachable vertical area. Both dimensions are DPI aware.
    // Measure the actual adjacent Deck surface, NOT merely the monitor.
    // A heading directly against the grid leaves zero outward flyout room
    // even if there are hundreds of monitor pixels beyond that heading.
    internal static double FreeSideBeforeDeckV118(
        double monitorRoomPx,double gridEdgePx,double deckNearEdgePx)=>
        Math.Max(0,Math.Min(Math.Max(0,monitorRoomPx),
            deckNearEdgePx-gridEdgePx-8));

    internal static WindowPlacementV115 ChooseReachableFlyoutV118(
        double freeLeftPx,double freeRightPx,double freeTopPx,
        double freeBottomPx,double monitorWidthPx,double dpi,
        int windowCount,bool outwardRight,bool preferAbove)
    {
        if(!double.IsFinite(dpi) || dpi<=0)dpi=1;
        double left=Math.Max(0,freeLeftPx/dpi-12);
        double right=Math.Max(0,freeRightPx/dpi-12);
        double above=Math.Max(0,freeTopPx/dpi-12);
        double below=Math.Max(0,freeBottomPx/dpi-12);
        double monitorWidth=Math.Max(0,monitorWidthPx/dpi-24);
        double preferredSide=outwardRight?right:left;
        double otherSide=outwardRight?left:right;
        var direction=outwardRight?WindowDirectionV115.Right:
            WindowDirectionV115.Left;
        // Full-size outward-side popup has no intervening app tile.
        if(preferredSide>=260)
            return new(true,direction,Math.Min(276,preferredSide),292);
        bool up=above>=96 && monitorWidth>=128;
        bool down=below>=96 && monitorWidth>=128;
        double desired=Math.Min(292,Math.Max(104,34+Math.Max(1,windowCount)*30));
        bool upFits=up && above>=desired;
        bool downFits=down && below>=desired;
        // The hovered icon determines which vertical direction is shortest:
        // upper-row buttons open above; lower-row buttons open below.
        if(upFits || downFits)
        {
            bool chooseUp=upFits && (!downFits || preferAbove);
            double available=chooseUp?above:below;
            return new(true,chooseUp?WindowDirectionV115.Up:
                WindowDirectionV115.Down,Math.Min(276,monitorWidth),
                Math.Min(292,available));
        }
        // Small screens still support scrollable flyouts above/below.
        if(up || down)
        {
            bool chooseUp=up && (!down || (preferAbove && above>=96) ||
                (above>=below && !preferAbove && below<96));
            double available=chooseUp?above:below;
            return new(true,chooseUp?WindowDirectionV115.Up:
                WindowDirectionV115.Down,Math.Min(276,monitorWidth),
                Math.Min(292,available));
        }
        if(preferredSide>=128)
            return new(true,direction,Math.Min(276,preferredSide),292);
        if(otherSide>=128)
            return new(true,outwardRight?WindowDirectionV115.Left:
                WindowDirectionV115.Right,Math.Min(276,otherSide),292);
        return new(false,WindowDirectionV115.Left,0,0);
    }

    // Anchor the vertical flyout just outside the ENTIRE grid. It must be
    // reachable directly above/below the selected tile and must not cover
    // the Deck header on the adjacent side. Keep the original four-way
    // positioning for horizontal cases.
    // Constrain a vertical flyout between the real monitor and Deck.
    // If the deck is on the left, there may be less room on the
    // monitor's right side: make the flyout narrower instead of
    // clamping it across the deck.
    internal static double SafeVerticalWidthV118(
        double gridWidth,double workLeft,double workRight,
        bool deckToRight,double requestedWidth)=>
        Math.Min(requestedWidth,Math.Max(0,deckToRight
            ?gridWidth-8-workLeft:workRight-8));

    internal static Point WindowFlyoutReachableV118(
        WindowDirectionV115 direction,double targetWidth,double targetHeight,
        double popupWidth,double popupHeight,double rowTop,double alignX,
        double workLeft,double workRight,double workTop,double workBottom,
        bool deckToRight)
    {
        if(direction is WindowDirectionV115.Left or WindowDirectionV115.Right)
            return WindowFlyoutFourWay(direction,targetWidth,targetHeight,
                popupWidth,popupHeight,rowTop,alignX,
                workLeft,workRight,workTop,workBottom);
        double preferred=alignX-popupWidth/2;
        double besideDeck=deckToRight
            ?Math.Min(preferred,targetWidth-popupWidth-8)
            :Math.Max(preferred,8);
        double x=Math.Clamp(besideDeck,workLeft,
            Math.Max(workLeft,workRight-popupWidth));
        return new Point(x,direction==WindowDirectionV115.Up
            ?-popupHeight-6:targetHeight+6);
    }
    internal static Button MakeCloseButton(DeskSpaceRunningAppsV88.Entry window)
    {
        var button=new Button{
            Content="×",
            Width=22,MinWidth=22,Height=23,MinHeight=23,
            FontSize=14,FontWeight=FontWeights.Medium,
            Foreground=Brushes.LightSteelBlue,
            Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
            BorderThickness=new Thickness(0),
            Padding=new Thickness(0),
            VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(1,1,2,1),
            ToolTip="Close this window normally (unsaved work may prompt)"
        };
        button.Click+=(_,e)=>{
            e.Handled=true;
            if(DeskSpaceRunningAppsV88.RequestClose(window))
            {
                button.IsEnabled=false;
                button.ToolTip="Waiting for the application to close or ask to save";
                // A user can cancel an unsaved-work dialog. Do not leave
                // its close button permanently disabled in that case.
                var retry=new DispatcherTimer{
                    Interval=TimeSpan.FromSeconds(2)
                };
                retry.Tick+=(_,_)=>{
                    retry.Stop();
                    if(DeskSpaceRunningAppsV88.IsActiveWindow(window.Handle))
                    {
                        button.IsEnabled=true;
                        button.ToolTip="Close this window normally (unsaved work may prompt)";
                    }
                };
                retry.Start();
            }
        };
        return button;
    }

    internal static Border Add(StackPanel root,
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> apps,
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> windows,
        Action<IReadOnlyList<DeskSpaceRunningAppsV88.Entry>,
            IReadOnlyList<DeskSpaceRunningAppsV88.Entry>>? onLiveChanged=null,
        Action? onHoverClosed=null)
    {
        var heading=new Border{
            Background=new SolidColorBrush(Color.FromRgb(39,53,83)),
            Padding=new Thickness(7,5,6,5),Margin=new Thickness(1,2,1,2),
            CornerRadius=new CornerRadius(4),Cursor=Cursors.Hand
        };
        heading.Child=new TextBlock{
            Text="▸  Running Apps · "+apps.Count,FontSize=11,
            Foreground=Brushes.White,FontWeight=FontWeights.SemiBold
        };
        // No running apps means no category header; other shelf groups stay.
        if(apps.Count==0)return heading;
        // Explicit THREE-column coordinates, never a wrapping row.
        // The final single-window app belongs in the middle of row two,
        // not the left side as it was in the old WrapPanel.
        var iconRow=new Grid{Margin=new Thickness(3),Width=234};
        for(int col=0;col<3;col++)
            iconRow.ColumnDefinitions.Add(new ColumnDefinition{
                Width=new GridLength(78)});
        var appTiles=new Dictionary<string,FrameworkElement>(
            StringComparer.OrdinalIgnoreCase);
        var appBadges=new Dictionary<string,TextBlock>(
            StringComparer.OrdinalIgnoreCase);
        // Assign true row+column locations in-place; do NOT detach/reinsert
        // hovered elements or shift their hit targets while a flyout is open.
        void OrderIconTiles(bool outwardRight,
            IReadOnlyList<DeskSpaceRunningAppsV88.Entry> current)
        {
            var planned=PlanAppGridV117(appTiles.Keys.Select(name=>
                (Name:name,Count:current.Count(w=>
                    string.Equals(w.Name,name,StringComparison.OrdinalIgnoreCase)))),
                outwardRight);
            int rows=planned.Length==0?0:planned.Max(x=>x.Row)+1;
            while(iconRow.RowDefinitions.Count<rows)
                iconRow.RowDefinitions.Add(new RowDefinition{
                    Height=GridLength.Auto});
            while(iconRow.RowDefinitions.Count>rows)
                iconRow.RowDefinitions.RemoveAt(
                    iconRow.RowDefinitions.Count-1);
            foreach(var slot in planned)
                if(appTiles.TryGetValue(slot.Name,out var tile))
                {
                    Grid.SetRow(tile,slot.Row);
                    Grid.SetColumn(tile,slot.Column);
                }
        }
        int liveCount=windows.Count;
        var iconCard=new Border{
            Background=new SolidColorBrush(Color.FromRgb(32,44,71)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(71,85,121)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),
            Padding=new Thickness(5),
            Child=new ScrollViewer{
                MaxHeight=230,MaxWidth=250,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=iconRow
            }
        };
        var iconPopup=new Popup{
            PlacementTarget=heading,Placement=PlacementMode.Right,
            HorizontalOffset=0,VerticalOffset=0,
            StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.Fade,Child=iconCard
        };
        bool PreferOutwardRight()
        {
            try
            {
                var card=iconCard.PointToScreen(
                    new Point(iconCard.ActualWidth/2,0));
                var deck=heading.PointToScreen(
                    new Point(heading.ActualWidth/2,0));
                return deck.X<card.X;
            }
            catch(InvalidOperationException){return false;}
        }
        // Content width is constrained to actual free monitor space on hover.
        var windowItems=new StackPanel{MinWidth=0,MaxWidth=268};
        var windowCard=new Border{
            Background=new SolidColorBrush(Color.FromRgb(32,44,71)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(71,85,121)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),
            Padding=new Thickness(5),
            Child=new ScrollViewer{
                MaxHeight=292,MaxWidth=280,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=windowItems
            }
        };
        // Use the closest safe SIDE of the entire icon grid. Never
        // overlay any other application's hit target or force the
        // pointer through a second row of application buttons.
        var windowPopup=new Popup{
            PlacementTarget=iconCard,
            Placement=PlacementMode.Custom,
            HorizontalOffset=0,VerticalOffset=0,
            StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.None,Child=windowCard
        };
        iconCard.Tag=windowPopup;
        DeskSpaceMonitorFenceV153.TrackPopup(heading,iconPopup);
        DeskSpaceMonitorFenceV153.TrackPopup(iconCard,windowPopup);
        // Separate noninteractive thumbnail flyout: no deck resize and no
        // obstruction of app or window selections while the mouse is moving.
        var appPreview=new DeskSpaceWindowPreviewV100();
        var hoverIntent=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(170)
        };
        var closeDelay=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(340)
        };
        var liveTimer=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(220)
        };
        bool closeRequestedWhileHover=false;
        CursorPoint? positionAtClose=null;
        void PreserveHoverAfterClose()
        {
            // Keep both detached popup HWNDs alive even if an entry is
            // removed under the pointer, invalidating WPF IsMouseOver.
            closeRequestedWhileHover=true;
            positionAtClose=GetCursorPos(out var point)?point:null;
            closeDelay.Stop();
            closeDelay.Start();
        }
        void StopIntent(){hoverIntent.Stop();}
        // Choose the OUTWARD side of the icon grid rather than the side of
        // the hovered tile. Both monitor edges and DPI are recomputed on open.
        bool pendingRight=true;
        FrameworkElement? pendingIcon=null;
        DeskSpaceRunningAppsV88.Entry[] pendingWindows=Array.Empty<DeskSpaceRunningAppsV88.Entry>();
        string pendingName="";
        void OpenPending(bool confirmedPointerEntry=false)
        {
            hoverIntent.Stop();
            var anchor=pendingIcon;
            // Hover events already confirm the pointer is on this tile.
            // WPF may temporarily clear IsMouseOver as the detached Popup
            // appears; do not suppress the requested multiple-window menu.
            if(anchor==null || (!confirmedPointerEntry &&
                !anchor.IsMouseOver && !iconCard.IsMouseOver))return;
            KeepOpen();
            windowPopup.IsOpen=false;
            windowItems.Children.Clear();
            pendingWindows=pendingWindows.Where(x=>
                DeskSpaceRunningAppsV88.IsActiveWindow(x.Handle)).ToArray();
            if(pendingWindows.Length<=1)return;
            double rowTop=0;
            try { rowTop=Math.Max(0,anchor.TranslatePoint(
                new Point(0,0),iconCard).Y); }
            catch(InvalidOperationException){}
            // Use the real monitor's four free directions after the icon
            // popup has been positioned (and potentially flipped) by WPF.
            WindowPlacementV115 placement;
            double left=0,right=0,top=0,bottom=0;
            double alignX=0;
            bool deckToRight=true;
            try
            {
                var from=iconCard.PointToScreen(new Point(0,0));
                var far=iconCard.PointToScreen(
                    new Point(iconCard.ActualWidth,iconCard.ActualHeight));
                // The tile is measured inside the grid after layout.
                alignX=anchor.TranslatePoint(
                    new Point(anchor.ActualWidth/2,0),iconCard).X;
                var monitor=System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point(
                        (int)Math.Round((from.X+far.X)/2),
                        (int)Math.Round((from.Y+far.Y)/2))).WorkingArea;
                double dpi=VisualTreeHelper.GetDpi(iconCard).DpiScaleX;
                // The outside of the selected tile is the desired side;
                // if blocked by the deck or monitor edge, open above or
                // below this tile's entire grid, never across other icons.
                bool outwardRight=alignX>=iconCard.ActualWidth/2;
                double tileY=anchor.TranslatePoint(
                    new Point(0,anchor.ActualHeight/2),iconCard).Y;
                bool preferAbove=tileY<=iconCard.ActualHeight/2;
                deckToRight=heading.PointToScreen(new Point(
                    heading.ActualWidth/2,0)).X>(from.X+far.X)/2;
                var deckFrom=heading.PointToScreen(new Point(0,0));
                var deckFar=heading.PointToScreen(
                    new Point(heading.ActualWidth,0));
                double freeLeft=from.X-monitor.Left;
                double freeRight=monitor.Right-far.X;
                if(deckToRight)
                    freeRight=FreeSideBeforeDeckV118(
                        freeRight,far.X,deckFrom.X);
                else
                    freeLeft=FreeSideBeforeDeckV118(
                        freeLeft,deckFar.X,from.X);
                placement=ChooseReachableFlyoutV118(
                    freeLeft,freeRight,
                    from.Y-monitor.Top,monitor.Bottom-far.Y,
                    monitor.Width,dpi,pendingWindows.Length,outwardRight,
                    preferAbove);
                // Monitor bounds expressed as icon-card-relative WPF DIPs.
                left=(monitor.Left-from.X)/dpi+8;
                right=(monitor.Right-from.X)/dpi-8;
                top=(monitor.Top-from.Y)/dpi+8;
                bottom=(monitor.Bottom-from.Y)/dpi-8;
            }
            catch(InvalidOperationException)
            {
                return; // Never guess a placement over the deck.
            }
            if(!placement.CanOpen)
            {
                windowPopup.IsOpen=false;
                return;
            }
            var direction=placement.Direction;
            if(direction is WindowDirectionV115.Up or WindowDirectionV115.Down)
            {
                double safe=SafeVerticalWidthV118(iconCard.ActualWidth,
                    left,right,deckToRight,placement.WidthDip);
                if(safe<128)
                {
                    windowPopup.IsOpen=false;
                    return;
                }
                placement=placement with {WidthDip=safe};
            }
            windowCard.Width=placement.WidthDip;
            windowItems.MaxWidth=Math.Max(100,placement.WidthDip-12);
            ((ScrollViewer)windowCard.Child).MaxHeight=placement.MaxHeightDip;
            windowPopup.CustomPopupPlacementCallback=(size,target,offset)=>
                new[]{new CustomPopupPlacement(WindowFlyoutReachableV118(
                    direction,target.Width,target.Height,
                    size.Width,size.Height,rowTop,alignX,
                    left,right,top,bottom,deckToRight),
                    PopupPrimaryAxis.None)};
            windowPopup.Placement=PlacementMode.Custom;
            windowPopup.HorizontalOffset=0;
            windowPopup.VerticalOffset=0;
            windowItems.Children.Add(new TextBlock{
                Text=pendingName+" · "+pendingWindows.Length+" windows",
                FontSize=11,Foreground=Brushes.LightSteelBlue,
                FontWeight=FontWeights.Medium,
                Margin=new Thickness(6,3,6,6)
            });
            foreach(var window in pendingWindows)
            {
                var selected=window;
                var row=new DockPanel{LastChildFill=true,
                    Tag=selected.Handle,
                    Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
                    Margin=new Thickness(2,1,2,1)};
                var close=MakeCloseButton(selected);
                close.Click+=(_,_)=>appPreview.Close();
                close.Click+=(_,_)=>PreserveHoverAfterClose();
                DockPanel.SetDock(close,Dock.Right);
                row.Children.Add(close);
                var choice=new Button{
                    Content=selected.WindowTitle,FontSize=11,
                    Foreground=Brushes.White,
                    Background=Brushes.Transparent,
                    BorderThickness=new Thickness(0),
                    HorizontalContentAlignment=HorizontalAlignment.Left,
                    Padding=new Thickness(7,5,7,5),
                    MaxWidth=229
                };
                choice.Click+=(_,_)=>{
                    Hide();
                    DeskSpaceRunningAppsV88.Focus(selected);
                };
                row.Children.Add(choice);
                appPreview.Bind(choice,windowCard,()=>selected);
                windowItems.Children.Add(row);
            }
            windowPopup.IsOpen=true;
        }
        hoverIntent.Tick+=(_,_)=>OpenPending();
        void Hide()
        {
            closeRequestedWhileHover=false;
            positionAtClose=null;
            bool wasOpen=iconPopup.IsOpen;
            liveTimer.Stop();
            hoverIntent.Stop();
            closeDelay.Stop();
            appPreview.Close();
            windowPopup.IsOpen=false;
            iconPopup.IsOpen=false;
            if(ReferenceEquals(activeIconPopup,iconPopup))
                activeIconPopup=null;
            if(wasOpen)onHoverClosed?.Invoke();
        }

        // Reconcile only real HWNDs: a posted WM_CLOSE is not proof that
        // an application has closed. This is deliberately lightweight
        // (no Process.GetProcesses, I/O, or main-deck rerender).
        void Reconcile()
        {
            var live=RemainingWindows(windows,
                DeskSpaceRunningAppsV88.IsActiveWindow);
            if(live.Count==liveCount)return;
            appPreview.Close();
            liveCount=live.Count;
            var groups=DeskSpaceRunningAppsV88.GroupVisible(live);
            foreach(var app in apps)
            {
                var matches=DeskSpaceRunningAppsV88.WindowsFor(app,live);
                if(!appTiles.TryGetValue(app.Name,out var tile))continue;
                if(matches.Count==0)
                {
                    iconRow.Children.Remove(tile);
                    appTiles.Remove(app.Name);
                    appBadges.Remove(app.Name);
                }
                else if(appBadges.TryGetValue(app.Name,out var badge))
                    badge.Text=matches.Count>1?
                        matches.Count+" windows":"";
            }
            foreach(var row in windowItems.Children.OfType<DockPanel>().
                Where(x=>x.Tag is IntPtr hwnd &&
                    !live.Any(w=>w.Handle==hwnd)).ToArray())
                windowItems.Children.Remove(row);
            var activeMatches=live.Where(x=>string.Equals(
                x.Name,pendingName,StringComparison.OrdinalIgnoreCase)).
                ToArray();
            pendingWindows=activeMatches;
            if(windowItems.Children.OfType<TextBlock>().FirstOrDefault()
                is TextBlock counter)
                counter.Text=pendingName+" · "+activeMatches.Length+" windows";
            if(activeMatches.Length==0)windowPopup.IsOpen=false;
            if(heading.Child is TextBlock label)
                label.Text="▸  Running Apps · "+groups.Count;
            OrderIconTiles(PreferOutwardRight(),live);
            // Update the deck count without destroying the hovered popup.
            if(groups.Count==0)Hide();
            onLiveChanged?.Invoke(groups,live);
        }
        liveTimer.Tick+=(_,_)=>Reconcile();
        bool Hovered()=>heading.IsMouseOver ||
            iconCard.IsMouseOver || windowCard.IsMouseOver;
        closeDelay.Tick+=(_,_)=>{
            // WM_CLOSE may remove a row/tile directly beneath the mouse.
            // Keep the menu visible until the pointer physically moves,
            // not merely because the WPF hover hit target disappeared.
            if(closeRequestedWhileHover)
            {
                if(positionAtClose.HasValue &&
                   GetCursorPos(out var cursor) &&
                   ShouldRetainAfterClose(positionAtClose.Value.X,
                       positionAtClose.Value.Y,cursor.X,cursor.Y))
                    return;
                closeRequestedWhileHover=false;
                positionAtClose=null;
            }
            if(Hovered())closeDelay.Stop();
            else Hide();
        };
        void KeepOpen()=>closeDelay.Stop();
        void CloseLater(){closeDelay.Stop();closeDelay.Start();}
        heading.MouseEnter+=(_,_)=>{
            KeepOpen();
            iconPopup.IsOpen=true;
            activeIconPopup=iconPopup;
            Reconcile();
            // WPF decides whether the parent grid opens left or right.
            // Reorder after that decision; grouped tiles stay on its outer edge.
            iconCard.Dispatcher.BeginInvoke(new Action(()=>
                OrderIconTiles(PreferOutwardRight(),windows)),
                System.Windows.Threading.DispatcherPriority.Loaded);
            if(iconPopup.IsOpen)liveTimer.Start();
        };
        heading.MouseLeave+=(_,_)=>CloseLater();
        iconCard.MouseEnter+=(_,_)=>KeepOpen();
        iconCard.MouseLeave+=(_,_)=>{StopIntent();CloseLater();};
        windowCard.MouseEnter+=(_,_)=>{StopIntent();KeepOpen();};
        windowCard.MouseLeave+=(_,_)=>CloseLater();

        // Width corresponds to exactly three 78-DIP tiles per row.
        iconRow.Width=234;
        foreach(var app in apps)
        {
            var matches=DeskSpaceRunningAppsV88.WindowsFor(app,windows).ToArray();
            if(matches.Length==0)matches=new[]{app};
            var picture=new StackPanel{Width=68};
            var source=DeskSpaceApplicationIconsV65.Get(app.Path);
            if(source!=null)picture.Children.Add(new Image{
                Source=source,Width=22,Height=22,
                Margin=new Thickness(0,0,0,2),
                HorizontalAlignment=HorizontalAlignment.Center
            });
            else picture.Children.Add(new TextBlock{
                Text="▣",FontSize=18,Foreground=Brushes.LightSteelBlue,
                TextAlignment=TextAlignment.Center
            });
            picture.Children.Add(new TextBlock{
                Text=app.Name,FontSize=10,Foreground=Brushes.White,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis
            });
            if(matches.Length>1)
            {
                var badge=new TextBlock{
                    Text=matches.Length+" windows",FontSize=9,
                    Foreground=Brushes.LightSteelBlue,
                    TextAlignment=TextAlignment.Center
                };
                picture.Children.Add(badge);
                appBadges[app.Name]=badge;
            }
            var appButton=new Button{
                Content=picture,Width=76,MinHeight=49,
                Margin=new Thickness(1),Padding=new Thickness(2),
                Foreground=Brushes.White,
                Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
                BorderThickness=new Thickness(0)
            };
            var choices=matches;
            // Single-window apps have a direct thumbnail. For a grouped
            // app, let the user choose the exact window first, then hover
            // its individual row for an accurate preview.
            appPreview.Bind(appButton,iconCard,()=>
                choices.Length==1 &&
                DeskSpaceRunningAppsV88.IsActiveWindow(choices[0].Handle)?
                    choices[0]:null);
            appButton.MouseEnter+=(_,_)=>{
                KeepOpen();
                StopIntent();
                var remaining=choices.Where(x=>
                    DeskSpaceRunningAppsV88.IsActiveWindow(x.Handle)).ToArray();
                if(remaining.Length<=1)
                {
                    windowPopup.IsOpen=false;
                    return;
                }
                pendingIcon=appButton;
                pendingWindows=remaining;
                pendingName=app.Name;
                // The final side is calculated when the flyout opens,
                // after WPF has actually placed the parent grid.
                pendingRight=true;
                // A pointer quickly crossing another app toward an open
                // window list must not select that intervening app.
                // Open immediately on actual hover. A 170ms delay could
                // cancel when the cursor crossed a child icon or the detached
                // parent Popup briefly lost its IsMouseOver WPF state.
                OpenPending(confirmedPointerEntry:true);
            };
            appButton.Click+=(_,_)=>{
                var remaining=choices.Where(x=>
                    DeskSpaceRunningAppsV88.IsActiveWindow(x.Handle)).ToArray();
                if(remaining.Length>1)
                {
                    pendingIcon=appButton;
                    pendingWindows=remaining;
                    pendingName=app.Name;
                    // Also allow clicking the tile to reveal its windows:
                    // no more inert clicks on "4 windows" app icons.
                    OpenPending(confirmedPointerEntry:true);
                    return;
                }
                if(remaining.Length!=1)return;
                Hide();
                DeskSpaceRunningAppsV88.Focus(remaining[0]);
            };
            if(choices.Length==1)
            {
                // A one-window app can be closed directly from its icon.
                // Multi-window apps expose separate X controls per window.
                var tile=new Grid{Width=78};
                tile.Children.Add(appButton);
                var close=MakeCloseButton(choices[0]);
                close.Click+=(_,_)=>appPreview.Close();
                close.Click+=(_,_)=>PreserveHoverAfterClose();
                close.Width=17;close.MinWidth=17;
                close.Height=17;close.MinHeight=17;
                close.FontSize=12;
                close.HorizontalAlignment=HorizontalAlignment.Right;
                close.VerticalAlignment=VerticalAlignment.Top;
                close.Margin=new Thickness(0,0,3,0);
                tile.Children.Add(close);
                iconRow.Children.Add(tile);
                appTiles[app.Name]=tile;
            }
            else
            {
                iconRow.Children.Add(appButton);
                appTiles[app.Name]=appButton;
            }
        }
        OrderIconTiles(false,windows);
        // Separate Popup HWNDs must close when the old deck is hidden
        // or its contents are regenerated.
        heading.Unloaded+=(_,_)=>{
            appPreview.Dispose();
            Hide();
        };
        heading.IsVisibleChanged+=(_,_)=>{
            if(!heading.IsVisible)Hide();
        };
        heading.Tag=iconPopup; // WPF test instrumentation; no layout effect.
        root.Children.Add(heading);
        return heading;
    }
}
