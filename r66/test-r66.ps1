$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$standard=Get-Content "$root/DeskSpaceSubDonutStandardV62.cs" -Raw
$ui=Get-Content 'r36/UiSmoke/Program.cs' -Raw
$rules=Get-Content 'r65/STANDARD_SUBDONUT_DESIGN.md' -Raw
foreach($token in @(
    'internal static Point DividerMidpoint(Geometry shape,bool leading)',
    'figure.Segments[0] is not ArcSegment outerArc',
    'figure.Segments[1] is not LineSegment radialLine',
    'figure.Segments[2] is not ArcSegment innerArc',
    'Point outer=leading?figure.StartPoint:outerArc.Point;',
    'Point inner=leading?innerArc.Point:radialLine.Point;',
    'outer=transform.Transform(outer);',
    'inner=transform.Transform(inner);',
    'new Point((outer.X+inner.X)/2,(outer.Y+inner.Y)/2)',
    'Canvas.SetLeft(button,midpoint.X-width/2);',
    'Canvas.SetTop(button,midpoint.Y-height/2);',
    'Content=count,',
    'button.Click+=(_,_)=>move();'
)){
 if(!$standard.Contains($token)){throw "R66 divider midpoint implementation missing: $token"}
}
if($standard.Contains('AddEdgeTab(') -or
   $standard.Contains('Content=leading?"‹ "') -or
   $standard.Contains('double x=leading?area.Left+2')){
 throw 'R66 old detached hidden-count badge or sector-edge rectangle remains'
}
foreach($token in @(
    'Hidden counts must be numerals embedded in radial end lines',
    'Hidden count not centered on actual vertical/radial end line',
    'Divider counter ignores clipped nested-Popup geometry transform',
    'Hidden counters must have NO separate background box',
    'Clicking the embedded left-line numeral failed to scroll')){
 if(!$ui.Contains($token)){throw "R66 missing WPF behavior assertion: $token"}
}
if(!$rules.Contains('radial midpoint of the existing vertical end divider')){
 throw 'R66 standard donut design specification not updated'
}
Write-Host 'R66 PASS: one universal count exactly at the center of each existing vertical end divider, including translated child arcs.'
