$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorSessionsV154.cs'
$s=[IO.File]::ReadAllText($p)
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'ShouldGuardV215(bool visible,WindowState state)',
 'if(CanGuard()){guard.Start();ScheduleMove();}',
 'else {guard.Stop();moveGuard.Stop();}',
 'if(!CanGuard()){guard.Stop();return;}',
 'widget.StateChanged+=stateChanged;',
 'widget.StateChanged-=stateChanged;',
 'widget.IsVisibleChanged+=visible;',
 'widget.IsVisibleChanged-=visible;',
 'session.Guard=guard;',
 'UpdateGuard();',
 'GuardOwnedWindows(widget);'
)){
 if(!$s.Contains($needle)){throw ("R215 lifecycle integration missing "+$needle)}
}
if(!$t.Contains('TestMonitorIdleGuardV215();')){
 throw 'R215 native idle monitor regression missing'
}
if($s.Contains('session.Guard=guard;'+[string][char]10+'        guard.Start();')){
 throw 'R215 no unconditional background guard startup'
}
Write-Host 'R215 STATIC PASS: event-based monitor fallback guard suspension and restart.'
