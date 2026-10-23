$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($token in @(
  'private static ControlTemplate NumberOnlyTemplate(Color ink)',
  'Color rim=ink.R>190?Color.FromRgb(22,31,52):Colors.White;',
  'new Point(-1,-1),new Point(0,-1),new Point(1,-1)',
  'new Point(-1,1),new Point(0,1),new Point(1,1)',
  'AddInk(rim,point.X,point.Y);',
  'AddInk(ink,0,0);',
  'root.SetValue(Panel.BackgroundProperty,Brushes.Transparent);',
  'double width=Math.Max(19,6+count.Length*10),height=19;',
  'Canvas.SetLeft(button,midpoint.X-width/2);',
  'Canvas.SetTop(button,midpoint.Y-height/2);'
)){
  if(!$src.Contains($token)){throw "R69 missing glyph-only outline: $token"}
}
if($src.Contains('new Border{') -or
   $src.Contains('Background=wedge.Fill')){
  throw 'R69 separate number badge reintroduced'
}
foreach($token in @(
  'subdonut-purple-new-white-background',
  'R69 hidden digit disappears into white outside purple end wedge',
  'darkInkOutside>=4',
  'R69 number moved off its actual divider midpoint',
  'R69 clear numeral must still click-to-scroll',
  'R69 WPF PASS: full purple end-line count readable against white background without a badge.'
)){
  if(!$smoke.Contains($token)){throw "R69 missing live WPF visibility check: $token"}
}
Write-Host 'R69 source PASS: dark-outline light-glyph and reverse, no badge, white-background pixel test required.'
