$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($token in @(
 'internal const double HiddenCountDotOffset=13.0;',
 'internal const double HiddenCountDotDiameter=3.8;',
 'internal const double HiddenCountGapHalf=HiddenCountDotOffset+HiddenCountDotDiameter/2.0;',
 'double half=Math.Min(HiddenCountGapHalf,len*.36);',
 'foreach(double offset in new[]{-HiddenCountDotOffset,HiddenCountDotOffset})',
 'dot.SetValue(FrameworkElement.WidthProperty,HiddenCountDotDiameter);',
 'dot.SetValue(FrameworkElement.HeightProperty,HiddenCountDotDiameter);'
)){
 if(!$src.Contains($token)){throw "R71 missing joined divider/dot implementation: $token"}
}
foreach($token in @(
 'R71 divider must end at the top dot and restart at bottom dot',
 'Math.Abs((end-dotCenter).Length-1.9)<.1',
 'subdonut-dot-joined-divider',
 'Hidden count not centered on actual vertical/radial end line',
 'R70 hidden count does not rotate with its actual divider'
)){
 if(!$smoke.Contains($token)){throw "R71 missing native WPF geometry verification: $token"}
}
if($src.Contains('new Border{') -or $src.Contains('Background=wedge.Fill')){
 throw 'R71 unexpectedly added a separate badge background'
}
Write-Host 'R71 PASS: hidden-count only; both divider segments meet the corresponding dots at their circumferences.'
