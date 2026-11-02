$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs'
$shelf='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $shelf)){
    throw 'R318: verified DeskSpace monitor or shelf source missing'
}
$utf=[Text.UTF8Encoding]::new($false)
$contents=[IO.File]::ReadAllText($source).Replace(([string][char]13+[string][char]10),[string][char]10)
function Replace-R318([string]$old,[string]$replacement,[string]$label) {
    $lf=[string][char]10
    $crlf=[string][char]13+$lf
    $old=$old.Replace($crlf,$lf)
    $replacement=$replacement.Replace($crlf,$lf)
    $hits=([regex]::Matches($script:contents,[regex]::Escape($old))).Count
    if($hits -ne 1){throw "R318 ${label}: expected one verified source anchor, found $hits"}
    $script:contents=$script:contents.Replace($old,$replacement)
}
Replace-R318 @'
        if(content==null)return 156;
'@ @'
        // Hover-only Running Apps has no inline icon grid. Reserve only
        // the real header/row height, not an empty 156px viewport.
        if(content==null)return 48;
'@ 'content-null default'
Replace-R318 @'
        return Math.Clamp(Math.Max(156,
            double.IsFinite(natural)?natural:156),156,520);
'@ @'
        // R314 prevented a 34px clipping loop but imposed an artificial
        // minimum on sparse decks. The actual unbounded child measure is
        // authoritative; 48 DIP is merely the safe empty/header fallback.
        // Growth is still capped for native monitor containment/scroll.
        return Math.Clamp(double.IsFinite(natural)?natural:48,48,520);
'@ 'natural content height'
Replace-R318 @'
            // A standard expanded Deck always has a usable viewport, even
            // when only Running Apps currently has data. Explicit manual
            // minimize is still allowed to shrink to the normal header.
            scroll.MinHeight=minimized?0:Math.Min(132,Math.Max(0,max-4));
            border.MinHeight=minimized?0:Math.Min(136,
                Math.Max(0,plan.Deck.Height/sy-6));
'@ @'
            // R318: never stretch a sparse deck to a blank 132/136 DIP
            // minimum. The header + collapsed Running Apps hover row are
            // already part of the measured StackPanel. Let content shrink
            // after removals, and expand only when real content is added.
            scroll.MinHeight=0;
            border.MinHeight=0;
'@ 'remove forced empty viewport'
$deck=[IO.File]::ReadAllText($shelf)
foreach($token in @(
    'DeskSpaceRunningAppsV88.AddHoverGroupLive(list,runningApps,runningWindows,',
    'DeskSpaceMonitorDockV190.RequestReflowV314(panel,openingV314)',
    'DeskSpaceMonitorDockV190.RegisterMinimizedV314(panel,()=>deckMinimized)',
    'var minimise=DeckHeaderButton(deckMinimized?'
)){
    if(!$deck.Contains($token)){throw "R318: essential legacy hover/expand behavior missing: $token"}
}
foreach($token in @(
    'Math.Clamp(double.IsFinite(natural)?natural:48,48,520)',
    'scroll.MinHeight=0;',
    'border.MinHeight=0;',
    'RequestReflowV314(',
    'FitBelowV292('
)){
    if(!$contents.Contains($token)){throw "R318 source missing $token"}
}
if($contents.Contains('Math.Min(132,') -or $contents.Contains('Math.Min(136,')){
    throw 'R318: forced empty Deck height remains'
}
[IO.File]::WriteAllText($source,$contents,$utf)
Write-Host 'R318: measured content controls deck height; Running Apps side hover preserved; no empty viewport; native monitor fencing retained.'
