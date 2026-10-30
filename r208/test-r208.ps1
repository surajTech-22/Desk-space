$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$colors=[IO.File]::ReadAllText("$root/DeskSpaceHighContrastV208.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'SystemParameters.HighContrast',
 'DeskSpaceHighContrastV208.Wedge(',
 'DeskSpaceHighContrastV208.Label(',
 'DeskSpaceHighContrastV208.Outline(',
 'DeskSpaceHighContrastV208.QuickAction(',
 'DeskSpaceHighContrastV208.QuickActionLabel(',
 'content.Foreground=SystemColors.WindowTextBrush;',
 'if(SystemParameters.HighContrast)return SystemColors.WindowBrush;'
)){
 if(!$source.Contains($needle)){throw ("R208 root contrast integration missing "+$needle)}
}
foreach($needle in @(
 'SystemColors.WindowBrush',
 'SystemColors.ControlBrush',
 'SystemColors.HighlightBrush',
 'SystemColors.WindowTextBrush',
 'SystemColors.HighlightTextBrush',
 'if(!highContrast)return selected?normalSelected:'
)){
 if(!$colors.Contains($needle)){throw ("R208 contrast palette missing "+$needle)}
}
if(!$smoke.Contains('TestHighContrastDonutV208();')){
 throw 'R208 native WPF regression not applied'
}
if($colors.Contains('Registry.') -or $colors.Contains('File.') -or
    $colors.Contains('Process.Start') -or $colors.Contains('StaticPropertyChanged')){
 throw 'R208 high contrast must not change OS settings, persist themes or register background watchers'
}
Write-Host 'R208 STATIC PASS: high contrast on all root sectors, centre and quick actions; no theme persistence.'
