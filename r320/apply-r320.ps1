$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$dock=Join-Path $root 'DeskSpaceMonitorDockV190.cs'
$shelf=Join-Path $root 'DeskSpaceDropShelfV15.cs'
if(!(Test-Path -LiteralPath $dock) -or !(Test-Path -LiteralPath $shelf)){throw 'R320 shipping Deck source missing'}
$utf=[Text.UTF8Encoding]::new($false)
$lf=[string][char]10
$crlf=[string][char]13+$lf
$d=[IO.File]::ReadAllText($dock).Replace($crlf,$lf)
$s=[IO.File]::ReadAllText($shelf).Replace($crlf,$lf)
function ReplaceExact([string]$target,[string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf)
 $new=$new.Replace($crlf,$lf)
 $current=Get-Variable -Name $target -ValueOnly
 $count=([regex]::Matches($current,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R320 "+$why+": expected exactly one anchor, found "+$count)}
 Set-Variable -Name $target -Value ($current.Replace($old,$new)) -Scope Script
}
ReplaceExact 'd' @'
    // The popup can be relocated by WPF, Windows or the monitor-fence after
'@ @'
    // The single monitor-local placement plan is used by both WPF initial
    // placement and native repair. The old native repair re-centered the
    // Deck and discarded the saved X offset: two different display points.
    internal static Rectangle CanonicalDeckRectV320(
        Rectangle widget,Rectangle work,int width,int height,int shiftX)
    {
        var plan=FitBelowV292(widget,work,width,height);
        if(!plan.Fits)return Rectangle.Empty;
        int x=Math.Clamp(plan.Deck.Left+shiftX,work.Left,
            Math.Max(work.Left,work.Right-plan.Deck.Width));
        return new Rectangle(x,plan.Deck.Top,plan.Deck.Width,plan.Deck.Height);
    }

    // The popup can be relocated by WPF, Windows or the monitor-fence after
'@ 'canonical placement planner'
ReplaceExact 'd' @'
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
'@ @'
            var safe=CanonicalDeckRectV320(ownerBounds,screen,
                widthPx,heightPx,shift.X);
            if(safe.IsEmpty)return Array.Empty<CustomPopupPlacement>();
            // WPF and native repair now use the exact same monitor-local
            // X/Y plan. Coordinates below are relative to this widget.
'@ 'WPF single planner'
ReplaceExact 'd' @'
            var corrected=NativeCorrectionV292(painted,
                Owner(owner,screen),screen);
            if(corrected.IsEmpty){popup.IsOpen=false;return;}
'@ @'
            // Reuse the WPF position, including this monitor's X offset.
            // Native correction must never snap a dragged Deck to center.
            var corrected=CanonicalDeckRectV320(Owner(owner,screen),
                screen,painted.Width,painted.Height,shift.X);
            if(corrected.IsEmpty){popup.IsOpen=false;return;}
'@ 'native correction'
ReplaceExact 'd' @'
        owner.SizeChanged+=(_,_)=>{Refresh();RepairLater();};
        owner.LocationChanged+=(_,_)=>{Refresh();RepairLater();};
'@ @'
        owner.SizeChanged+=(_,_)=>{Refresh();Reposition();RepairLater();};
        owner.LocationChanged+=(_,_)=>{Refresh();Reposition();RepairLater();};
'@ 'owner movement belongs only to Dock'
ReplaceExact 's' @'
        popup.CustomPopupPlacementCallback=(size,target,offset)=>{
            var widget=new Rect(host.Left,host.Top,
                host.ActualWidth>0?host.ActualWidth:host.Width,
                host.ActualHeight>0?host.ActualHeight:host.Height);
            var chosen=DeskSpaceRecentPlacementV82.Choose(
                widget,SystemParameters.WorkArea,size);
            return new[]{new CustomPopupPlacement(
                chosen,PopupPrimaryAxis.None)};
        };
'@ @'
        // R320: the Deck has ONE placement callback, assigned by
        // DeskSpaceMonitorDockV190.Attach below. Do not introduce the
        // obsolete primary-screen callback from RecentPlacementV82.
'@ 'obsolete callback'
ReplaceExact 's' @'
        host.LocationChanged+=Position;
        host.SizeChanged+=Position;
'@ @'
        // R320: Dock.Attach owns placement refresh on location/size.
'@ 'duplicate location handlers'
ReplaceExact 's' @'
    private void Position(object? sender,EventArgs e)
    {
        // A placement update is required on some WPF builds after moving a Window.

        if(!popup.IsOpen)return;
        popup.HorizontalOffset=(++placementNudge%2==0)?0:0.01;
    }
'@ '' 'second offset nudge'
ReplaceExact 's' @'
        host.LocationChanged-=Position;
        host.SizeChanged-=Position;
'@ @'
        // Dock-owned placement events are released with the host.
'@ 'second handler cleanup'
foreach($req in @('CanonicalDeckRectV320(ownerBounds,screen,',
 'CanonicalDeckRectV320(Owner(owner,screen),',
 'return new Rectangle(x,plan.Deck.Top,plan.Deck.Width,plan.Deck.Height);')){
 if(!$d.Contains($req)){throw ("R320 missing canonical placement "+$req)}
}
if($s.Contains('DeskSpaceRecentPlacementV82.Choose(') -or
 $s.Contains('popup.CustomPopupPlacementCallback=') -or
 $s.Contains('host.LocationChanged+=Position;')){
 throw 'R320 competing shelf placement point remains'
}
foreach($req in @('DeskSpaceMonitorDockV190.Attach(host,popup,panel);',
 'DeskSpaceMonitorFenceV153.TrackPopup(host,popup,',
 'DeskSpaceRunningAppsV88.AddHoverGroupLive(list,runningApps,runningWindows,')){
 if(!$s.Contains($req)){throw ("R320 old Deck functionality missing: "+$req)}
}
[IO.File]::WriteAllText($dock,$d,$utf)
[IO.File]::WriteAllText($shelf,$s,$utf)
Write-Host 'R320: one canonical Deck position; keep monitor-local X offset in WPF and native repair; remove obsolete second placement callback.'
