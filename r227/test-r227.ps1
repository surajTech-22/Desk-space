$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceUpdateSupervisor.ps1")
$p=[IO.File]::ReadAllText("$root/DeskSpaceReleasePushV133.cs")
$u=[IO.File]::ReadAllText("$root/DeskSpacePublicUpdaterV44.cs")
$m=[IO.File]::ReadAllText("$root/DeskSpaceReleaseAttemptV227.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($part in @(
 'R227 active supervisor owns install lock; duplicate exits without relaunch.',
 'if(!$ownsMutex){',
 'if($ownsMutex){',
 'R227 installed widget did not produce matching receipt',
 'R185 verified full-install snapshot',
 'Restore-R185Snapshot $install $snapshot $quarantine'
)){
 if(!$s.Contains($part)){throw ("R227 safe supervisor missing "+$part)}
}
if($s.Contains('if(!$ownsMutex){throw ''R185 another supervised install is active''}')){
 throw 'R227 mutex loser must not enter failure catch/relaunch'
}
foreach($part in @('DeskSpaceReleaseAttemptV227.ShouldDispatch(tag)',
 'R227 replayed release hint suppressed')){
 if(!$p.Contains($part)){throw ("R227 push replay gate missing "+$part)}
}
foreach($part in @('TimeSpan.FromMinutes(8)',
 'File.Move(tmp,Marker,true)',
 'new Mutex(false,@"Local\DeskSpace-ReleaseHint-R227")')){
 if(!$m.Contains($part)){throw ("R227 persistent release hint gate missing "+$part)}
}
foreach($part in @(
 'using(var existing=new Mutex(false,@"Local\DeskSpace-UpdateSupervisor-V185"))',
 'R227 active supervisor already owns installer',
 'throw new InvalidOperationException("R227 supervised installation already running;',
 'CompleteDeskSpaceUpdate.',
 'Application.Current.Shutdown();'
)){
 if(!$u.Contains($part)){throw ("R227 updater install mutex gate missing "+$part)}
}
if(!$t.Contains('TestSupervisorLoopGuardV227();')){
 throw 'R227 native WPF replay tests were not registered'
}
Write-Host 'R227 STATIC PASS: duplicate supervisors exit without relaunch, replay bounded and rollback/verification preserved.'
