$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$tray=[IO.File]::ReadAllText("$root/DeskSpaceTrayRecoveryV166.cs")
$hostCode=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
  'RegisterWindowMessage("TaskbarCreated")',
  'icon.Visible=false;',
  'icon.Visible=true;',
  'owner.Dispatcher.BeginInvoke(',
  'source.AddHook(hook);',
  'source.RemoveHook(hook);',
  'owner.Closed+=OnClosed;',
  'Interval=TimeSpan.FromSeconds(30)'
)){if(!$tray.Contains($token)){throw "R166 tray recovery missing: $token"}}
foreach($token in @(
  'DeskSpaceTrayRecoveryV166.Watch(this,tray)',
  '!DeskSpaceMultiMonitorV151.IsSatellite(this)'
)){if(!$hostCode.Contains($token)){throw "R166 primary-only integration missing: $token"}}
foreach($token in @(
  'TestTrayRecoveryV166();',
  'private static void TestTrayRecoveryV166()',
  'R166 Explorer recovery must re-register the SAME tray icon',
  'R166 real registered taskbar message must restore tray invisibly'
)){if(!$smoke.Contains($token)){throw "R166 native WPF regression missing: $token"}}
Write-Host 'R166 PASS: one primary tray, taskbar restart recovery and real WPF smoke installed.'
