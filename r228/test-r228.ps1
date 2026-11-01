$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$mon=[IO.File]::ReadAllText("$root/DeskSpaceMultiMonitorV151.cs")
$dock=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDockV190.cs")
$offset=[IO.File]::ReadAllText("$root/DeskSpaceDeckOffsetV228.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
if($mon.Contains('else PlaceIfOffscreen(replica,screen);')){
 throw 'R228 old magnetic position reset must not run during monitor reconciliation'
}
if(!$mon.Contains('DeskSpaceMonitorFenceV152.ClampNow(replica);')){
 throw 'R228 existing widget must respect its own native monitor fence'
}
foreach($needle in @(
 'DeskSpaceDeckOffsetV228.Read(Device())',
 'DeskSpaceDeckOffsetV228.Save(Device(),shift)',
 'startCursor=Forms.Cursor.Position;',
 'DeskSpaceDeckOffsetV228.Clamp(new(',
 'popup.PlacementTarget=owner;',

 'AvoidWidgetV282(',
 '(safe.Left-ownerBounds.Left)/sx',
 '(safe.Top-ownerBounds.Top)/sy'
)){
 if(!$dock.Contains($needle)){throw ("R228 Deck display binding missing "+$needle)}
}
# R289 replaces the legacy movable Y offset: vertical placement is
# locked below the owning widget and can never be manually shifted above.
if($dock.Contains('FitBelowV292('))
{
 if(!$dock.Contains('var deckX=Math.Clamp(plan.Deck.Left+shift.X,screen.Left,') -or
    !$dock.Contains('var corrected=NativeCorrectionV292(') -or
    !$dock.Contains('EnsureBelowV289()')){
   throw 'R292 physical Deck positioning must remain below clock'
 }
}
elseif($dock.Contains('CalculateBelowV289('))
{
 if(!$dock.Contains('var safe=RepairBelowV289(') -or
    !$dock.Contains('EnsureBelowV289()') -or
    $dock.Contains('var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,')){
  throw 'R289 Deck must remain below its circle regardless of saved Y offset'
 }
}
else
{
 if(!$dock.Contains('var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,')){
   throw 'Legacy R228 per-monitor Deck vertical offset missing'
 }
}

if(!$offset.Contains('widget-independent-displays') -and
   !$offset.Contains('deck-per-monitor-v228.json')){
 throw 'R228 per-screen persisted Deck state absent'
}
if(!$t.Contains('TestIndependentDeckAndWidgetPositionsV228();')){
 throw 'R228 native WPF tests not hooked up'
}
Write-Host 'R228 STATIC PASS: native widget coordinates and each independent monitor Deck placement persisted.'
