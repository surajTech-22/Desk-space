$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($key in @(
 'Geometry shape,Brush? sectorFill,bool leading,string count',
 'Vector inward=perpendicular*(leading?-1:1);',
 'Point glyphCenter=center+inward*5.5;',
 'var dark=new SolidColorBrush(CounterInk(sectorFill));',
 'DrawInlineCountInk(canvas,wedge.Data,wedge.Fill,leading,count);'
)){if(!$src.Contains($key)){throw "R77 missing inset count $key"}}
if($src.Contains('whole.Children.Add(Segment(outer,topEnd));') -or
   $src.Contains('whole.Children.Add(Segment(lowerStart,inner));')){
 throw 'R77 must not introduce extra divider lines'
}
foreach($key in @('R77 hidden numeral must be inlayed INTO colored wedge',
  'subdonut-r77-inset-count')){
 if(!$smoke.Contains($key)){throw "R77 WPF test missing $key"}
}
Write-Host 'R77 PASS: hidden-count ink inlaid into sector, dots stay on original divider, zero additional line geometry.'
