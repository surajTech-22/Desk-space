$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs')
foreach($needle in @(
 'Header="Reset Deck position on this monitor"',
 'ToolTip="Reset only this display',
 'shift=new DeskSpaceDeckOffsetV228.Offset(0,0);',
 'DeskSpaceDeckOffsetV228.Save(Device(),shift);',
 'border.ContextMenu=deckContext;',
 'Reposition();'
)){
 if(!$s.Contains($needle)){throw ('R234 missing monitor-specific reset: '+$needle)}
}
# R289 replaces the legacy movable Y offset: vertical placement is
# locked below the owning widget and can never be manually shifted above.
if($s.Contains('CanonicalDeckRectV320('))
{
 # R320: WPF and native correction MUST consume one offset-aware plan.
 # Reject any "fix" that merely removes the previous static tokens.
 if(!$s.Contains('var safe=CanonicalDeckRectV320(ownerBounds,screen,') -or
    !$s.Contains('var corrected=CanonicalDeckRectV320(Owner(owner,screen),') -or
    !$s.Contains('var plan=FitBelowV292(widget,work,width,height);') -or
    !$s.Contains('plan.Deck.Left+shiftX') -or
    !$s.Contains('shift=new DeskSpaceDeckOffsetV228.Offset(0,0);')){
    throw 'R320 WPF and native repair must share the same per-monitor below-only placement'
 }
}
elseif($s.Contains('FitBelowV292('))
{
 if(!$s.Contains('var deckX=Math.Clamp(plan.Deck.Left+shift.X,screen.Left,') -or
    !$s.Contains('NativeCorrectionV292(')){
   throw 'R292 monitor-local reset and below-only positioning missing'
 }
}
elseif($s.Contains('CalculateBelowV289('))
{
 if(!$s.Contains('var safe=RepairBelowV289(') -or
    !$s.Contains('EnsureBelowV289()') -or
    $s.Contains('var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,')){
  throw 'R289 Deck must remain below its circle regardless of saved Y offset'
 }
}
else
{
 if(!$s.Contains('var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,')){
   throw 'Legacy R228 per-monitor Deck vertical offset missing'
 }
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
