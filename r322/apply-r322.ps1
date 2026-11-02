$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$dock='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs'
if(!(Test-Path -LiteralPath $dock)){throw 'R322 expected shipping DeskSpace monitor dock source'}
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($dock).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$label) {
 $old=$old.Replace($crlf,$lf)
 $new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R322 "+$label+" anchor count="+$count)}
 $script:s=$script:s.Replace($old,$new)
}
One @'
    internal static Rectangle CanonicalDeckRectV320(
        Rectangle widget,Rectangle work,int width,int height,int shiftX)
    {
        var plan=FitBelowV292(widget,work,width,height);
        if(!plan.Fits)return Rectangle.Empty;
        int x=Math.Clamp(plan.Deck.Left+shiftX,work.Left,
            Math.Max(work.Left,work.Right-plan.Deck.Width));
        return new Rectangle(x,plan.Deck.Top,plan.Deck.Width,plan.Deck.Height);
    }
'@ @'
    // R322: saved shifts of +/-4096 are valid legacy data, but NOT valid
    // positions for a popup that belongs to its clock. Require at least
    // 80 physical pixels of horizontal overlap (or all of a small widget).
    internal static int MaxAttachedShiftV322(int widgetWidth,int deckWidth)
    {
        int overlap=Math.Min(Math.Max(1,widgetWidth),80);
        return Math.Max(0,((Math.Max(1,widgetWidth)+Math.Max(1,deckWidth))/2)
            -overlap);
    }
    internal static Rectangle CanonicalDeckRectV320(
        Rectangle widget,Rectangle work,int width,int height,int shiftX)
    {
        var plan=FitBelowV292(widget,work,width,height);
        if(!plan.Fits)return Rectangle.Empty;
        // Never use a saved or live drag offset as a second independent
        // screen coordinate. Only small movements relative to THIS clock.
        int limit=MaxAttachedShiftV322(plan.Widget.Width,plan.Deck.Width);
        int attachedShift=Math.Clamp(shiftX,-limit,limit);
        // A monitor-edge clamp can consume part of the original centering
        // offset. Enforce actual widget/deck overlap AFTER both screen and
        // saved-shift constraints; otherwise a left-edge secondary monitor
        // can still detach 80px despite the bounded relative shift.
        int overlap=Math.Min(Math.Max(1,plan.Widget.Width),80);
        int left=Math.Max(work.Left,
            plan.Widget.Left+overlap-plan.Deck.Width);
        int right=Math.Min(work.Right-plan.Deck.Width,
            plan.Widget.Right-overlap);
        if(left>right)return Rectangle.Empty;
        int x=Math.Clamp(plan.Deck.Left+attachedShift,left,right);
        return new Rectangle(x,plan.Deck.Top,plan.Deck.Width,plan.Deck.Height);
    }
'@ 'relative tether not monitor-wide'
One @'
        // Keep horizontal monitor-local manual preference where valid.
        int x=Math.Clamp(proposed.Left,work.Left,
            Math.Max(work.Left,work.Right-fit.Deck.Width));
        return new Rectangle(x,fit.Widget.Bottom+12,
            fit.Deck.Width,fit.Deck.Height);
'@ @'
        // Native monitor fence is a safety constraint, not a second Deck
        // positioning authority. Even an ancient saved screen-edge placement
        // must be tethered to its circle BEFORE the first painted frame.
        return CanonicalDeckRectV320(fit.Widget,work,
            proposed.Width,proposed.Height,proposed.Left-fit.Deck.Left);
'@ 'first-frame fence must respect tether'
One @'
            var saved=DeskSpaceDeckOffsetV228.Read(Device());
            // Legacy saved Y offsets are ignored; below-only is mandatory.
            shift=new DeskSpaceDeckOffsetV228.Offset(saved.X,0);
'@ @'
            var saved=DeskSpaceDeckOffsetV228.Read(Device());
            // R322: normalize legacy 4096px offsets in memory on load;
            // never overwrite saved data automatically. The next genuine
            // user drag saves a valid nearby offset for THIS monitor only.
            var ownerRect=Owner(owner,Work(owner));
            int deckWidth=Math.Max(180,(int)Math.Ceiling(
                border.ActualWidth>1?border.ActualWidth:240));
            int limit=MaxAttachedShiftV322(ownerRect.Width,deckWidth);
            shift=new DeskSpaceDeckOffsetV228.Offset(
                Math.Clamp(saved.X,-limit,limit),0);
'@ 'sanitize old saved X at load'
One @'
            shift=DeskSpaceDeckOffsetV228.Clamp(new(
                startOffset.X+now.X-startCursor.X,0));
'@ @'
            // Drag is horizontal and remains attached to its own widget;
            // prevent large stale or repeated movement from detaching Deck.
            var ownerRect=Owner(owner,Work(owner));
            int deckWidth=Math.Max(180,(int)Math.Ceiling(
                border.ActualWidth>1?border.ActualWidth:240));
            int limit=MaxAttachedShiftV322(ownerRect.Width,deckWidth);
            shift=new DeskSpaceDeckOffsetV228.Offset(
                Math.Clamp(startOffset.X+now.X-startCursor.X,-limit,limit),0);
'@ 'keep manual dragging attached'
foreach($required in @(
 'MaxAttachedShiftV322(plan.Widget.Width,plan.Deck.Width)',
 'CanonicalDeckRectV320(fit.Widget,work,',
 'Math.Clamp(saved.X,-limit,limit)',
 'Math.Clamp(startOffset.X+now.X-startCursor.X,-limit,limit)',
 'var safe=CanonicalDeckRectV320(ownerBounds,screen,',
 'var corrected=CanonicalDeckRectV320(Owner(owner,screen),'
)){
 if(!$s.Contains($required)){throw ("R322 missing guarantee "+$required)}
}
[IO.File]::WriteAllText($dock,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R322: deck horizontal coordinates now remain attached to its own clock in WPF, first-frame monitor fence and native repair. Huge legacy saved positions sanitized in memory; drag kept nearby.'
