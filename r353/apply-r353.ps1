$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
if(!(Test-Path -LiteralPath $path)){throw 'R353 cannot find shipping updater'}
$s=[IO.File]::ReadAllText($path)
$old=@'
        Log("Verified "+tag+" handed to independent installer supervisor");
        timer?.Stop();
        // Do not forcibly shut down only the updater owner here; R230's
'@
$new=@'
        Log("Verified "+tag+" handed to independent installer supervisor");
        // R353: Do NOT stop automatic checks until the actual app exits.
        // The supervisor is independent and may fail to close an existing
        // Workspace, or may exit before installation. Stopping the timer here
        // permanently stranded that still-running app with no retry.
        // R191 cached verified bytes plus the R310 supervisor mutex make
        // subsequent ticks safe: while supervisor is running they defer,
        // and once it exits unsuccessfully the next tick retries the handoff
        // WITHOUT downloading another untrusted installer.
        // On successful exit, the app process/timer naturally terminate.
        Log("R353 update supervisor handoff accepted; auto retry checks remain active until normal app exit.");
        // Do not forcibly shut down only the updater owner here; R230's
'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
    throw 'R353 exact updater timer stop anchor missing or repeated'
}
$s=$s.Replace($old,$new)
if($s.Contains('timer?.Stop()')){throw 'R353 updater still stops automatic checks after supervisor launch'}
if(!$s.Contains('DeskSpaceVerifiedUpdateQueueV191.Peek(Channel)') -or
   !$s.Contains('SupervisorBusyV310Exception') -or
   !$s.Contains('new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)}')){
    throw 'R353 must retain R191 cache, supervisor busy safety, and auto-check timer'
}
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R353: verified updater remains responsive after failed independent install handoff.'
