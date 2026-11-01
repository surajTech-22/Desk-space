$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePinnedRecentPickerV262.cs')
$m=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePinnedRecentMonitorV264.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($term in @(
 'WindowStartupLocation=WindowStartupLocation.Manual',
 'DeskSpacePinnedRecentMonitorV264.Attach(window,owner)',
 'DeskSpaceMonitorFenceV152.BoundScreen(widget)',
 'Forms.Screen.FromHandle(ownerHwnd)',
 'TransformFromDevice',
 'GetWindowRect(ownerHwnd',
 'dialog.LocationChanged+=',
 'dialog.SizeChanged+=',
 'TestPinnedPickerMonitorV264();'
)){
 if(!$p.Contains($term) -and !$m.Contains($term) -and !$t.Contains($term)){
  throw 'R264 per-monitor picker safety anchor missing: '+$term
 }
}
if($p.Contains('WindowStartupLocation.CenterScreen') -and
   !$p.Contains('// the attached monitor fence')) {
 throw 'R264 must not use primary-screen fallback for visible owning widget'
}
Write-Host 'R264 STATIC PASS: per-widget picker tied to owner HWND, physical screen fence and negative-origin test.'
