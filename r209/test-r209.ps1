$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$policy=[IO.File]::ReadAllText("$root/DeskSpaceReducedMotionV209.cs")
$widgetCode=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$sub=[IO.File]::ReadAllText("$root/DeskSpaceSubDonutStandardV62.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'SystemParameters.ClientAreaAnimation',
 'SystemParameters.MenuAnimation',
 'SystemParameters.HighContrast',
 'surface.BeginAnimation(UIElement.OpacityProperty,null)',
 'surface.Opacity=1;',
 'surface.RenderTransform=Transform.Identity',
 'internal static bool ShouldAnimate('
)){
 if(!$policy.Contains($needle)){throw ("R209 reduced-motion policy missing "+$needle)}
}
foreach($needle in @(
 'if(!DeskSpaceReducedMotionV209.Enabled())',
 'DeskSpaceReducedMotionV209.Reset(surface);',
 'DeskSpaceReducedMotionV209.Reset(immediate);'
)){
 if(!$widgetCode.Contains($needle)){throw ("R209 host animation guard missing "+$needle)}
}
if(!$sub.Contains('DeskSpaceReducedMotionV209.Reset(canvas)')){
 throw 'R209 Information/Excel child-ring animation must obey OS motion preference'
}
if(!$smoke.Contains('TestReducedMotionV209();')){
 throw 'R209 native reduced-motion acceptance not connected'
}
if($policy.Contains('Registry.') -or $policy.Contains('DispatcherTimer') -or
   $policy.Contains('File.') -or $policy.Contains('Process.Start(')){
 throw 'R209 must neither modify Windows settings nor poll for motion changes'
}
Write-Host 'R209 STATIC PASS: existing morph and child-ring motion honor OS preferences, no new watcher.'
