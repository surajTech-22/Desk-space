$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$apps=Get-Content "$root/DeskSpaceApplicationsV55.cs" -Raw
$search=Get-Content "$root/DeskSpaceSearchV11.cs" -Raw
foreach($t in @(
 'ApplicationsParentCenter',
 'DeskSpaceSubArcGeometryV48.Sector(',
 'DeskSpaceSubArcGeometryV48.Bounds(shapes)',
 'DeskSpaceSubArcGeometryV48.Clip(shapes[i],bounds)',
 'DeskSpaceSubArcGeometryV48.PopupOffset(owner,bounds)',
 'canvas.Width=bounds.Width+margin*2',
 'BaseInner=202,BaseOuter=255,SubInner=257,SubOuter=310',
 'ShowApplicationPinSearch()',
 'DeskSpaceSearchV11.RequestApplicationPinMode()',
 'openSearch();')){
 if(!$apps.Contains($t)){throw "R61 Apps donut missing $t"}
}
if($apps.Contains('new Microsoft.Win32.OpenFileDialog') -or
   $apps.Contains('pinPalette') -or $apps.Contains('Width=680')){
 throw 'R61 must not use a separate file picker or oversized pin search popup'
}
foreach($t in @(
 'RequestApplicationPinMode()',
 'Interlocked.Exchange(ref nextApplicationPinMode,0)==1',
 'appPinMode?"pin":"open"',
 'DeskSpaceSearchIndexV13.SnapshotApplications()',
 'field.Focus();Keyboard.Focus(field);',
 'status.Text="Pinned application: "+hit.Name',
 'appRefresh=new DispatcherTimer',
 'field.TextChanged+=')){
 if(!$search.Contains($t)){throw "R61 native Search-pin mode missing $t"}
}
$ui=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($t in @('TestApplicationsArcAndPinSearch()',
  'applications-aligned-parent','DeskSpaceSubArcGeometryV48',
  'nextApplicationPinMode','Search was not opened from the wheel')){
 if(!$ui.Contains($t)){throw "R61 WPF smoke missing $t"}
}
Write-Host 'R61 PASS: tightly cropped Information-style Applications arc and circle-morphing Search-based application pinning.'
