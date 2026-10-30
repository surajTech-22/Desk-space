$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$fence=[IO.File]::ReadAllText("$root/DeskSpaceMonitorFenceV152.cs")
$multi=[IO.File]::ReadAllText("$root/DeskSpaceMultiMonitorV151.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @('ResolveActiveDeviceV214(string desired,',
 'IReadOnlyList<string> connected,string primary)',
 'screens.Select(s=>s.DeviceName).ToArray()',
 'return screens.FirstOrDefault(s=>string.Equals(')){
 if(!$fence.Contains($needle)){throw ("R214 monitor fallback missing "+$needle)}
}
if($fence.Contains('Assigned[window]=fallback.DeviceName;')){
 throw 'R214 must never replace intended monitor identity during disconnection'
}
$first=$multi.IndexOf('new WindowInteropHelper(replica).EnsureHandle();')
$bind=$multi.IndexOf('DeskSpaceMonitorFenceV152.PlaceNewSatellite(replica,screen);')
$show=$multi.IndexOf('replica.Show();')
if($first -lt 0 -or $bind -le $first -or $show -le $bind){
 throw 'R214 a satellite may be shown only after HWND creation and monitor binding'
}
if(([regex]::Matches($multi,[regex]::Escape('replica.Show();'))).Count -ne 1){
 throw 'R214 must not show the satellite twice'
}
if(!$multi.Contains('DeskSpaceMonitorFenceV152.ClampNow(primary);')){
 throw 'R214 must re-home reconnected primary on topology change'
}
if(!$smoke.Contains('TestMonitorTopologyV214();')){
 throw 'R214 native monitor topology acceptance is missing'
}
Write-Host 'R214 STATIC PASS: reconnect identity, single first paint, native HWND-before-Show and dock geometry.'
