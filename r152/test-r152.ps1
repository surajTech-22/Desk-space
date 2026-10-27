$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$manager=[IO.File]::ReadAllText("$root/DeskSpaceMultiMonitorV151.cs")
$fence=[IO.File]::ReadAllText("$root/DeskSpaceMonitorFenceV152.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')

foreach($token in @(
 'DeskSpaceMonitorFenceV152.Bind(widget,',
 'var original=DeskSpaceMonitorFenceV152.BoundScreen(primary)',
 'DeskSpaceMonitorFenceV152.PlaceNewSatellite(replica,screen);',
 'else DeskSpaceMonitorFenceV152.ClampNow(replica);')){
 if(!$manager.Contains($token)){throw "R152 manager integration missing: $token"}
}
if(!$widget.Contains('DeskSpaceMonitorFenceV152.ClampIfBound(this)')){
 throw 'R152 existing window-fit path must invoke bound-monitor fence'
}
foreach($token in @(
 'GetWindowRect(hwnd,out var rect)',
 'SetWindowPos(hwnd,IntPtr.Zero',
 'screen.WorkingArea',
 'Assigned[window]=screenName',
 'window.LocationChanged+=',
 'window.SizeChanged+=',
 'window.StateChanged+=',
 'Forms.Screen.PrimaryScreen??screens[0]',
 'private static readonly HashSet<WidgetShellWindow> Moving=new();',
 'var pos=ClampRectangle(',
 'internal static void PlaceNewSatellite(')){
 if(!$fence.Contains($token)){throw "R152 missing fence contract: $token"}
}
if($fence.Contains('Application.Current.Shutdown(') -or
   $fence.Contains('widget-v3.json",')){
 throw 'R152 may not shut down the host or change widget data'
}
foreach($token in @('TestMonitorFenceV152();',
 'private static void TestMonitorFenceV152()',
 '"right and bottom seam"',
 '"second-screen edge"',
 '"negative screen offsets"')){
 if(!$smoke.Contains($token)){throw "R152 WPF test missing: $token"}
}
Write-Host 'R152 PASS: real widget monitor fence connected; synthetic seam, edge, and negative-coordinate tests installed.'
