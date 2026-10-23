$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=Get-Content 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs' -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
$guide=Get-Content 'r65/STANDARD_SUBDONUT_DESIGN.md' -Raw
foreach($required in @(
    'internal static double DividerAngle(Geometry shape,bool leading)',
    'while(angle>90)angle-=180;',
    'while(angle< -90)angle+=180;',
    'RenderTransform=new RotateTransform(',
    'DividerAngle(wedge.Data,leading)',
    'new[]{-13.0,13.0}',
    'typeof(System.Windows.Shapes.Ellipse)',
    'new TranslateTransform(0,offset)',
    'double half=Math.Min(16.5,len*.36);',
    'Background=Brushes.Transparent'
)){
    if(!$src.Contains($required)){throw "R70 orientation/dot feature missing: $required"}
}
if($src.Contains('new Border{') -or $src.Contains('Background=wedge.Fill')){
    throw 'R70 must not reintroduce a visible badge'
}
foreach($required in @(
    'R70 hidden count does not rotate with its actual divider',
    'R70 hidden count must have exactly one dot above and below',
    'R70 hidden-count dots must follow the divider, not the page',
    'R70 end count must follow +New divider orientation',
    'subdonut-oriented-hidden-counts',
    'subdonut-purple-oriented-count-dots'
)){
    if(!$smoke.Contains($required)){throw "R70 WPF regression missing: $required"}
}
if(!$guide.Contains('Only overflow/hidden-count numbers')){
    throw 'R70 must be scoped to hidden counts only'
}
Write-Host 'R70 PASS: rotate hidden count only, add exactly two dots on its own divider, retain unmodified module labels/other numbers.'
