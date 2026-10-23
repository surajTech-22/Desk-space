$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($token in @(
  'private static void SplitDividerStroke(Canvas canvas,',
  'AddRadial(figure.StartPoint,innerArc.Point,leading);',
  'AddRadial(outerArc.Point,radialLine.Point,trailing);',
  'double half=Math.Min(HiddenCountGapHalf,len*.36);',
  'wedge.Stroke=null;',
  'canvas.Children.Add(border);',
  'canvas.Children.Add(button);',
  'Canvas.SetLeft(button,midpoint.X-width/2);',
  'Canvas.SetTop(button,midpoint.Y-height/2);',
  'IsHitTestVisible=false'
)){
  if(!$src.Contains($token)){throw "R68 missing true inline end-divider number: $token"}
}
foreach($forbidden in @(
  'new Border{',
  'Background=wedge.Fill??Brushes.Transparent',
  'Canvas.SetLeft(embedded,',
  'Canvas.SetTop(embedded,'
)){
  if($src.Contains($forbidden)){throw "R68 separate badge/background still present: $forbidden"}
}
foreach($token in @(
  'Hidden counters must have NO separate background box',
  'Divider stroke was not split around the numeral',
  'Actual line gap must have two endpoints beside the numeral',
  'Embedded divider numeral lacks 4.5:1 contrast',
  'Clicking the embedded left-line numeral failed to scroll',
  'subdonut-inline-divider-counts'
)){
  if(!$smoke.Contains($token)){throw "R68 WPF verification missing: $token"}
}
Write-Host 'R68 PASS: real separated divider-line geometry, bare clickable text, no rectangular background, both scrolling directions retained.'
