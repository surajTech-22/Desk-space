$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs')
foreach($needle in @(
 'Header="Reset Deck position on this monitor"',
 'ToolTip="Reset only this display',
 'shift=new DeskSpaceDeckOffsetV228.Offset(0,0);',
 'DeskSpaceDeckOffsetV228.Save(Device(),shift);',
 'border.ContextMenu=deckContext;',
 'Reposition();',
 'var deckX=Math.Clamp(result.X+shift.X,screen.Left,',
 'var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,'
)){
 if(!$s.Contains($needle)){throw ('R234 missing monitor-specific reset: '+$needle)}
}
$start=$s.IndexOf('resetDeckThisMonitor.Click+=')
$end=$s.IndexOf('var deckContext=', $start)
if($start -lt 0 -or $end -le $start){throw 'R234 reset click handler missing'}
$handler=$s.Substring($start,$end-$start)
if($handler.Contains('Clear(') -or $handler.Contains('Delete(') -or
   $handler.Contains('Remove(') -or $handler.Contains('prefs.Left')){
 throw 'R234 reset must never touch staged files or shared widget location'
}
Write-Host 'R234 STATIC PASS: Deck context reset applies to owner monitor only and retains shared files.'
