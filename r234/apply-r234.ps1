$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        popup.PlacementTarget=owner;
        popup.Placement=PlacementMode.Custom;
'@.Replace($crlf,$lf)
$new=@'
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
            DeskSpaceDeckOffsetV228.Save(Device(),shift);
            Reposition();
        };
        var deckContext=border.ContextMenu ?? new ContextMenu();
        deckContext.Items.Add(resetDeckThisMonitor);
        border.ContextMenu=deckContext;
        popup.PlacementTarget=owner;
        popup.Placement=PlacementMode.Custom;
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R234 Deck context insertion expected 1 anchor, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R234: right-click either Deck to reset only that monitor placement; shared held files untouched.'
