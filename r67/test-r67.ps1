$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
$guide=Get-Content 'r65/STANDARD_SUBDONUT_DESIGN.md' -Raw
foreach($token in @(
  'internal static Color CounterInk(Brush? sectorFill)',
  'darkMinimum>=whiteMinimum?dark:white',
  'samples.Min(c=>InkContrast(dark,c))',
  'samples.Min(c=>InkContrast(white,c))',
  'private static ControlTemplate NumberOnlyTemplate(Color ink)',
  'new RelativeSource(RelativeSourceMode.TemplatedParent)',
  'glyph.SetBinding(TextBlock.TextProperty,',
  'Color rim=ink.R>190?Color.FromRgb(22,31,52):Colors.White;',
  'new Point(-1,-1),new Point(0,-1),new Point(1,-1)',
  'root.AppendChild(glyph);',
  'double width=Math.Max(21,8+count.Length*10),height=39;',
  'FontSize=13.5,FontWeight=FontWeights.Bold',
  'Background=Brushes.Transparent,',
  'Template=NumberOnlyTemplate(color),',
  'SplitDividerStroke(canvas,wedges[0],true,false);',
  'wedge.Stroke=null;',
  'canvas.Children.Add(border);',
  'Canvas.SetLeft(button,midpoint.X-width/2);',
  'Canvas.SetTop(button,midpoint.Y-height/2);')){
  if(!$src.Contains($token)){throw "R67 missing blended numeral treatment: $token"}
}
if($src.Contains('new Border{') -or
   $src.Contains('Background=wedge.Fill') -or
   $src.Contains('new Thickness(1.5)') -or
   $src.Contains('new CornerRadius(5)')){
   throw 'R67 must not reintroduce an outlined number badge'
}
foreach($token in @(
  'Hidden counters must have NO separate background box',
  'Counter must be a clear numeral without a rectangular button plate',
  'Divider stroke was not split around the numeral',
  'Actual line gap must have two endpoints beside the numeral',
  'Embedded divider numeral lacks 4.5:1 contrast',
  'subdonut-inline-divider-counts')){
  if(!$smoke.Contains($token)){throw "R67 WPF visual assertion missing: $token"}
}
if(!$guide.Contains('adaptive WCAG-style contrast')){
  throw 'R67 design standard lacks blending and readability rules'
}
Write-Host 'R67 PASS: blended divider numeral, smaller matching fill, chrome-free text and adaptive readability source checks.'
