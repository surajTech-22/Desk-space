$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceStableHoverV93.cs')
foreach($required in @('PreferRight(','PlacementMode.Left',
  'PlacementTarget=iconCard','hoverIntent.Start()',
  'hoverIntent.Stop()','Interval=TimeSpan.FromMilliseconds(170)',
  'iconCard.MouseLeave+=','windowCard.MouseEnter+=',
  'appButton.PointToScreen(','iconCard.PointToScreen(')){
 if(!$s.Contains($required)){throw "R95 missing hover intent or side placement: $required"}
}
if($s.Contains('Placement=PlacementMode.Bottom') -or
   $s.Contains('windowPopup.PlacementTarget=appButton')){
 throw 'R95 must not place window list below icons or over app hit targets'
}
Write-Host 'R95 PASS: hover intent debounce and near-side window flyout without icon obstruction'
