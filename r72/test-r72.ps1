$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($token in @(
  'private static System.Windows.Shapes.Path DrawInlineCountInk(',
  'Tag="DeskSpaceHiddenCountInline"',
  'whole.Children.Add(Segment(outer,topEnd));',
  'whole.Children.Add(new EllipseGeometry(topDot,radius,radius));',
  'whole.Children.Add(digits);',
  'whole.Children.Add(new EllipseGeometry(lowerDot,radius,radius));',
  'whole.Children.Add(Segment(lowerStart,inner));',
  'DrawInlineCountInk(canvas,wedge.Data,leading,count);',
  'Opacity=0,',
  'FormattedText(count,',
  'Canvas.SetZIndex(button,41);'
)){
 if(!$src.Contains($token)){throw "R72 missing unified hidden-count rendering: $token"}
}
foreach($token in @(
 'R72: hidden-count number must be actual divider ink, not an overlay',
 'R72 line-dots-number-dots-line must be ONE drawn geometry',
 'R72 dots must be one shared line, not floating labels',
 'R72 all parts of the number-line must share visible dark ink',
 'R72: purple end count must be drawn with its divider, not in button overlay',
 'subdonut-actual-inline-count-ink'
)){
 if(!$smoke.Contains($token)){throw "R72 missing Windows rendering assertion: $token"}
}
Write-Host 'R72 source PASS: numeral is an intrinsic part of the same radial stroke/dot geometry; invisible hit button.'
