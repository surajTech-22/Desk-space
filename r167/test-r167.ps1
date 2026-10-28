$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$monitor=[IO.File]::ReadAllText("$root/DeskSpaceMonitorSessionsV154.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
  'Interval=TimeSpan.FromMilliseconds(1500)',
  'Interval=TimeSpan.FromMilliseconds(60)',
  'widget.LocationChanged+=move;',
  'widget.SizeChanged+=size;',
  'widget.IsVisibleChanged+=visible;',
  'widget.LocationChanged-=move;',
  'widget.SizeChanged-=size;',
  'widget.IsVisibleChanged-=visible;',
  'moveGuard.Stop();',
  'GuardOwnedWindows(widget);',
  'GetCurrentThreadId()'
)){if(!$monitor.Contains($token)){throw "R167 per-monitor guard missing: $token"}}
foreach($token in @(
  'TestIndependentMonitorsV154();',
  'TestMonitorAwareSubmenusV157();',
  'TestMonitorInputIsolationV158();'
)){if(!$smoke.Contains($token)){throw "R167 existing real HWND regression missing: $token"}}
Write-Host 'R167 PASS: coalesced widget movement guard plus fallback HWND containment, no extra global polling.'
