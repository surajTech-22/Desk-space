$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
if(!(Test-Path -LiteralPath $path)){throw 'R353 cannot find shipping updater'}
$s=[IO.File]::ReadAllText($path)
$old='        timer?.Stop();'
$new=@'
        // R353: Do NOT stop automatic checks when the external supervisor
        // accepts the verified installer handoff. An aborted WPF shutdown,
        // installer failure, or unacknowledged ready receipt can otherwise
        // strand this still-running DeskSpace process without any future
        // update checks. The process itself ends on successful installation.
        //
        // R191 cached verified bytes plus the R310 supervisor mutex make
        // repeated ticks safe: concurrent attempts defer until the owner
        // finishes and a subsequent tick retries only the trusted candidate.
        Log("R353 update supervisor accepted; periodic update retry remains enabled until normal app exit.");
'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
    throw 'R353 expected exactly one automatic timer stop in actual shipping supervisor'
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
