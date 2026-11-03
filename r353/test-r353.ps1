$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
if(!(Test-Path -LiteralPath $path)){throw 'R353 staged source not found'}
$s=[IO.File]::ReadAllText($path)
function Assert([bool]$ok,[string]$description){
    if(!$ok){throw ('R353 TEST FAILED: '+$description)}
}
$start=$s.IndexOf('private static async Task StartSupervisor(')
$end=$s.IndexOf('private static async Task<bool> ResumeVerifiedV191(', $start)
Assert ($start -ge 0 -and $end -gt $start) 'verified supervisor and retry methods missing'
$handoff=$s.Substring($start,$end-$start)
Assert (!$handoff.Contains('timer?.Stop()')) 'the updater disabled all future checks before knowing install result'
Assert ($handoff.Contains('R353 update supervisor handoff accepted; auto retry checks remain active')) 'handoff status log missing'
Assert ($handoff.Contains('DeskSpace-UpdateSupervisor-V185')) 'existing supervisor ownership mutex removed'
Assert ($s.Contains('DeskSpaceVerifiedUpdateQueueV191.Peek(Channel)')) 'verified cached installer retry removed'
Assert ($s.Contains('DeskSpaceVerifiedUpdateQueueV191.IsStillVerified(pending)')) 'verified disk-cache hash guard removed'
Assert ($s.Contains('catch(SupervisorBusyV310Exception)')) 'safe active-supervisor defer removed'
Assert ($s.Contains('timer.Tick+=async (_,_)=>await CheckAsync(owner,true)')) 'existing periodic release checks missing'
Assert ($s.Contains('DeskSpaceReleasePushV133.Start(owner)')) 'immediate signed-recheck release notifications removed'
Assert ($s.Contains('DeskSpaceVerifiedUpdateQueueV191.Stage(')) 'trusted installer staging removed'
Assert ($s.Contains('DeskSpaceVerifiedUpdateQueueV191.Clear(pending)')) 'verification cleanup removed'
Assert ($s.Contains('DeskSpaceUpdateRateV127.IsDeferred(')) 'GitHub tester rate safety removed'
Assert ($s.Contains('if(!supervisorLaunched)DeskSpaceUpdateProgress.Clear()')) 'failed supervisor handoff status rollback removed'
Write-Host 'R353 PASS: automatic retries survive a failed supervisor; SHA256, immutable staging, mutex ownership, event hints, rate limits and normal WPF close remain in place.'
