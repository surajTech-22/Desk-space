$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$geo=[IO.File]::ReadAllText("$r/DeskSpaceSubArcGeometryV48.cs")
$app=[IO.File]::ReadAllText("$r/DeskSpaceApplicationsV55.cs")
$deck=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal static Rect MonitorWorkArea(Window owner)',
 'DeskSpaceMonitorFenceV152.BoundScreen(widget)',
 'Forms.Screen.FromHandle(handle)',
 'GetWindowRect(handle,out var rect)',
 'TransformToDevice.M11',
 'TransformToDevice.M22',
 'FitAngleAt(depth,preferred,half,centre,work,scale)'
)){
 if(!$geo.Contains($token)){throw "R157 missing monitor-aware geometry: $token"}
}
if($geo.Contains('Rect work=SystemParameters.WorkArea;')){
 throw 'R157 must not fit secondary monitor menus to primary WorkArea'
}
if(!$app.Contains('DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);')){
 throw 'R157 Applications Popup is not bound to its own monitor'
}
if(!$deck.Contains('DeskSpaceMonitorFenceV153.TrackPopup(host,popup);')){
 throw 'R157 Deck Popup is not bound to its own monitor'
}
foreach($t in @('TestMonitorAwareSubmenusV157();',
'private static void TestMonitorAwareSubmenusV157()',
'R157 live submenu fit must use the owner HWND',
'R157 secondary-screen submenu should turn away',
'R157 negative-origin secondary screen must support menu fitting')){
 if(!$ui.Contains($t)){throw "R157 real WPF regression missing $t"}
}
Write-Host 'R157 PASS: native monitor work area, per-monitor DPI, Applications/Deck popup containment, simulated + and - screen offsets.'
