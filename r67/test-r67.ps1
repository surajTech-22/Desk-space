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
  'new DropShadowEffect{',
  'Opacity=.25',
  'double width=Math.Max(15,4+count.Length*9),height=17;',
  'FontSize=12.5,FontWeight=FontWeights.Bold',
  'Background=Brushes.Transparent,',
  'Template=NumberOnlyTemplate(color),',
  'Background=wedge.Fill??Brushes.Transparent',
  'Canvas.SetLeft(embedded,midpoint.X-width/2);',
  'Canvas.SetTop(embedded,midpoint.Y-height/2);',
  'CornerRadius=new CornerRadius(0)')){
  if(!$src.Contains($token)){throw "R67 missing blended numeral treatment: $token"}
}
if($src.Contains('new Thickness(1.5)') -or
   $src.Contains('new CornerRadius(5)')){
   throw 'R67 must not reintroduce an outlined number badge'
}
foreach($token in @(
  'Counter divider mask must blend exactly with its wedge fill',
  'Counter must be a clear numeral without a rectangular button plate',
  'Embedded divider numeral lacks 4.5:1 contrast',
  'subdonut-blended-divider-counts')){
  if(!$smoke.Contains($token)){throw "R67 WPF visual assertion missing: $token"}
}
if(!$guide.Contains('adaptive WCAG-style contrast')){
  throw 'R67 design standard lacks blending and readability rules'
}
Write-Host 'R67 PASS: blended divider numeral, smaller matching fill, chrome-free text and adaptive readability source checks.'
