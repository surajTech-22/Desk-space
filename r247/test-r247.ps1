$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMultiMonitorV151.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'ResumeDelaysV247={1500,5000,12000}',
 'ResumeDelayForAttemptV247(int attempt)',
 'if(args.Mode!=PowerModes.Resume)return;',
 'SystemEvents.PowerModeChanged+=OnPowerModeV247;',
 'SystemEvents.PowerModeChanged-=OnPowerModeV247;',
 'resumeTimerV247?.Stop();',
 'resumeTimerV247=null;',
 'if(!ReferenceEquals(primary,owner))return;',
 'ReadPreference();',
 'Reconcile();',
 'TestMonitorResumePolicyV247();'
)){
 if(!$s.Contains($needle) -and !$t.Contains($needle))
 {throw ('R247 missing event-driven wake/reconnect safety: '+$needle)}
}
if($s.Contains('Interval=TimeSpan.FromSeconds(3)')){
 throw 'R247 monitor subsystem cannot restore frequent polling'
}
Write-Host 'R247 STATIC PASS: finite power resume retries, main widget lifecycle, no increased polling.'
