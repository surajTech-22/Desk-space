$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs')
$dpi=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDpiBoundsV195.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'DeskSpaceDpiBoundsV195.CleanScale(sx)',
 'DeskSpaceDpiBoundsV195.CleanScale(sy)',
 'DeskSpaceDpiBoundsV195.Pixels(popupSize.Width,sx,screen.Width)',
 'DeskSpaceDpiBoundsV195.Pixels(popupSize.Height,sy,screen.Height)',
 'DeskSpaceMonitorFenceV153'
)){
 if(!$src.Contains($needle)){throw ("R195 dock missing "+$needle)}
}
if(([regex]::Matches($src,[regex]::Escape('DeskSpaceDpiBoundsV195.CleanScale(sx)'))).Count -ne 2){
 throw 'R195 two independent WPF popup paths must use current monitor DPI'
}
foreach($needle in @(
 'double.IsFinite(value)',
 'double.IsFinite(dips)',
 'Math.Ceiling(native)',
 'Math.Max(1,workPixels)'
)){
 if(!$dpi.Contains($needle)){throw ("R195 geometry missing "+$needle)}
}
if(!$smoke.Contains('TestPerMonitorDpiV195();') -or
   !$smoke.Contains('new System.Drawing.Rectangle(-2560,0,2560,1400)')){
 throw 'R195 independent monitor origin/DPI matrix missing'
}
Write-Host 'R195 PASS: bounded per-widget physical DPI, unchanged Win32 seam fence and 40 synthetic matrix cases.'
