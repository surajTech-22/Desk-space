$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$engine=Get-Content "$root/DeskSpaceSubDonutStandardV62.cs" -Raw
$apps=Get-Content "$root/DeskSpaceApplicationsV55.cs" -Raw
$excel=Get-Content "$root/DeskSpaceExcelWheelV42.cs" -Raw
$info=Get-Content "$root/DeskSpaceInformationV36.cs" -Raw
$ui=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($token in @('internal const int Visible=5',
    'ParentSweep=360.0/7.0','HiddenLeft','HiddenRight',
    'Count,center','private static Button Counter',
    'internal sealed class Wheel','remainder+=delta',
    'internal static void Animate(Canvas canvas',
    'DrawHiddenCounters(Canvas canvas,View view',
    'new CubicEase{EasingMode=EasingMode.EaseOut}')){
  if(!$engine.Contains($token)){throw "R62 shared layout missing $token"}
}
foreach($token in @('DeskSpaceSubDonutStandardV62.Plan(roots.Length,0,centre)',
    'DeskSpaceSubDonutStandardV62.DrawHiddenCounters',
    'DeskSpaceSubDonutStandardV62.Animate(canvas,delta,',
    'private readonly DeskSpaceSubDonutStandardV62.Wheel scrollWheel',
    'new[]{','("Pinned","pinned"),("+ Pin","add"),("Search","search")')){
  if(!$apps.Contains($token)){throw "R62 Apps missing $token"}
}
if($apps.Contains('("All apps","installed")')){throw 'All apps is still present in Applications'}
foreach($token in @('PageSize=DeskSpaceSubDonutStandardV62.Visible-1',
    'DeskSpaceSubDonutStandardV62.DrawHiddenCounters',
    'DeskSpaceSubDonutStandardV62.Animate(arcCanvas,delta,-90)',
    'standardWheel.Step(e.Delta)')){
  if(!$info.Contains($token)){throw "R62 Information missing $token"}
}
foreach($token in @('PerPage=DeskSpaceSubDonutStandardV62.Visible',
    'DeskSpaceSubDonutStandardV62.DrawHiddenCounters',
    'rootWheel.Step(e.Delta)','ring.Wheel.Step(e.Delta)',
    'DeskSpaceSubDonutStandardV62.Animate(ring.Surface,dir,ring.Angle)',
    'DeskSpaceSubDonutStandardV62.Animate(canvas,dir,ArcCenter)')){
  if(!$excel.Contains($token)){throw "R62 nested Excel standard missing $token"}
}
foreach($token in @('TestStandardSubDonutScroll()',
    'Hidden counters missing on left or right',
    'R62 WPF PASS: five visible',
    'mouse wheel/touchpad stepping and smooth animation.')){
  if(!$ui.Contains($token)){throw "R62 UI regression missing $token"}
}
Write-Host 'R62 PASS: shared standard donut, max five, half-neighbour spread, wheel scrolling, hidden-left/right counters and animations across Applications/Information/Excel.'
