$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function ReplaceOnce([string]$path,[string]$before,[string]$after,[string]$label)
{
    $crlf=[string][char]13+[string][char]10;$lf=[string][char]10
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $n=([regex]::Matches($s,[regex]::Escape($before))).Count
    if($n -ne 1){throw "R118 $($label) expected one anchor, found $n"}
    [IO.File]::WriteAllText($path,$s.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
$source="$root/DeskSpaceStableHoverV93.cs"
$helper=@'
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

'@
ReplaceOnce $source '    internal static Button MakeCloseButton(' ($helper+'    internal static Button MakeCloseButton(') 'insert four-way reachable geometry'
ReplaceOnce $source @'
            double alignX=0;
'@ @'
            double alignX=0;
            bool deckToRight=true;
'@ 'remember deck side without changing popups'
ReplaceOnce $source @'
                // Place the list outward from the PARTICULAR multi-window
                // tile, not toward the centre of its sibling app icons.
                bool outwardRight=alignX>=iconCard.ActualWidth/2;
                placement=ChooseFourWayPlacement(
                    from.X-monitor.Left,monitor.Right-far.X,
                    from.Y-monitor.Top,monitor.Bottom-far.Y,
                    monitor.Width,dpi,pendingWindows.Length,outwardRight);
'@ @'
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
'@ 'choose flyout from hovered tile, not opposite grid edge'
ReplaceOnce $source @'
                new[]{new CustomPopupPlacement(WindowFlyoutFourWay(
                    direction,target.Width,target.Height,
                    size.Width,size.Height,rowTop,alignX,
                    left,right,top,bottom),PopupPrimaryAxis.None)};
'@ @'
                new[]{new CustomPopupPlacement(WindowFlyoutReachableV118(
                    direction,target.Width,target.Height,
                    size.Width,size.Height,rowTop,alignX,
                    left,right,top,bottom,deckToRight),
                    PopupPrimaryAxis.None)};
'@ 'place popup near tile and away from the deck'
$smoke='r36/UiSmoke/Program.cs'
ReplaceOnce $smoke @'
            TestRunningAppsGridV117();
'@ @'
            TestRunningAppsGridV117();
            TestReachableFlyoutV118();
'@ 'execute actual placement regression on WPF smoke runner'
$method=@'
    private static void TestReachableFlyoutV118()
    {
        var cls=Product.GetType("DeskSpace.DeskSpaceStableHoverV93",true)!;
        var choose=cls.GetMethod("ChooseReachableFlyoutV118",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        object Choice(double left,double right,double top,double bottom,
            int count,bool outward,bool preferAbove)=>
            choose.Invoke(null,new object[]{
                left,right,top,bottom,888d,1d,count,outward,preferAbove})!;
        string Dir(object v)=>v.GetType().GetProperty("Direction")!.
            GetValue(v)!.ToString()!;
        bool Allowed(object v)=>(bool)v.GetType().
            GetProperty("CanOpen")!.GetValue(v)!;
        // Regression from user's screenshot: Excel tile is at right edge,
        // deck blocks the outward side, but the entire left of the grid
        // has room. Leftward popup was detached across two sibling tiles.
        Assert(Dir(Choice(412,16,250,280,2,true,true))=="Up",
            "R118 Excel top-row popup must open ABOVE, not LEFT across File Explorer");
        Assert(Dir(Choice(412,16,250,280,2,true,false))=="Down",
            "R118 bottom-row multi-window app opens BELOW whole grid");
        Assert(Dir(Choice(20,450,250,280,4,true,true))=="Right",
            "R118 keep real outward-side flyout when space is available");
        Assert(Dir(Choice(500,16,25,24,4,true,true))=="Left",
            "R118 opposite side allowed only as vertical-space last resort");
        Assert(!Allowed(Choice(22,15,20,40,4,true,true)),
            "R118 no available screen area must block flyout");
        var point=cls.GetMethod("WindowFlyoutReachableV118",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var directionType=cls.GetNestedType("WindowDirectionV115",
            BindingFlags.NonPublic)!;
        Point Where(string direction,bool deckRight)
        {
            object enumValue=Enum.Parse(directionType,direction);
            return (Point)point.Invoke(null,new object[]{
                enumValue,252d,120d,276d,110d,5d,208d,
                -410d,220d,-250d,320d,deckRight})!;
        }
        var boundary=cls.GetMethod("FreeSideBeforeDeckV118",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        double Remaining(double room,double from,double deck)=>
            (double)boundary.Invoke(null,new object[]{room,from,deck})!;
        Assert(Remaining(573,1347,1340)==0,
            "R118 Deck directly beside right grid edge blocks outward popup");
        Assert(Remaining(500,600,810)==202,
            "R118 stop at Deck surface despite available monitor width");
        Assert(Remaining(300,800,1200)==300,
            "R118 preserve available room when no Deck obstruction");
        var above=Where("Up",true);
        Assert(above.Y+110<=-6 &&
               above.X+276<=252-7 &&
               above.X+276>=208,
            "R118 first-row flyout must touch top edge, remain beside Excel and avoid Deck");
        var below=Where("Down",true);
        Assert(below.Y>=126 && below.X+276<=252-7,
            "R118 lower-row flyout must stay below all app tiles and away from Deck");
        var leftDeck=Where("Up",false);
        Assert(leftDeck.X>=8,
            "R118 reverse-side deck must not be covered by vertical popup");
        Console.WriteLine("R118 PASS: screenshot regression, four-way reachability, no app-grid/deck overlap.");
    }

'@
ReplaceOnce $smoke '    private static void TestRunningAppsGridV117()' ($method+'    private static void TestRunningAppsGridV117()') 'inject WPF screen-position tests'
Write-Host 'R118: anchored up/down flyouts avoid crossing sibling apps and deck; geometry tests installed.'
