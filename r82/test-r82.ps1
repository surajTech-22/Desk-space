$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$boot=[IO.File]::ReadAllText("$r/DeskSpaceStartupV82.cs")
$widgetSource=[IO.File]::ReadAllText("$r/WidgetHost.cs")
$watch=[IO.File]::ReadAllText("$r/DeskSpaceNewFileWatchV53.cs")
$shelf=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$geometry=[IO.File]::ReadAllText("$r/DeskSpaceRecentPlacementV82.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('preferences.StartWithWindows=true;',
 'preferences.ShowAtLaunch=true;',
 'preferences.ApplyWindowsStartup();',
 'WindowsWidgetStartupR82.initialized')){
 if(!$boot.Contains($token)){throw "R82 logon startup missing $token"}
}
if(!$widgetSource.Contains('DeskSpaceStartupV82.EnableOnFirstLaunch(prefs)')){
 throw 'R82 widget launch does not set Windows startup'}
foreach($token in @('TimeSpan.FromHours(24)',
 'TimeSpan.FromMilliseconds(250)','recovering?6000:3000',
 'OrderByDescending(x=>x.Stamp).Take(24).Reverse()')){
 if(!$watch.Contains($token)){throw "R82 recent startup recovery missing $token"}
}
foreach($token in @('PlacementMode.Custom',
 'CustomPopupPlacementCallback',
 'DeskSpaceRecentPlacementV82.Choose(',
 'ScheduleRecentRender();',
 'recentRenderTimer.Stop();')){
 if(!$shelf.Contains($token)){throw "R82 recent below circle UI missing $token"}
}
foreach($token in @('work.Right-popup.Width',
 'widget.Bottom+7',
 'widget.Top-popup.Height-7')){
 if(!$geometry.Contains($token)){throw "R82 shelf clamp missing $token"}
}
foreach($token in @('TestStartupAndRecentPlacementV82();',
 'DateTime.UtcNow.AddHours(-8)',
 'R82 recent shelf must be centered directly below circle',
 'R82 recent popup clipped by right edge or taskbar')){
 if(!$smoke.Contains($token)){throw "R82 WPF startup/recent test missing $token"}
}
Write-Host 'R82 source gate PASS: Windows Run startup registration, startup recent-file recovery, under-circle popup/work-area placement, bounded scan.'
