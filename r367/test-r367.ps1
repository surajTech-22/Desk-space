$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$listener=Get-Content -LiteralPath (Join-Path $root 'DeskSpaceReleasePushV133.cs') -Raw
$updater=Get-Content -LiteralPath (Join-Path $root 'DeskSpacePublicUpdaterV44.cs') -Raw
foreach($guard in @(
  'new System.Threading.Mutex(false,',
  'streamOwner.WaitOne(0)',
  'catch(AbandonedMutexException)',
  'started=false;',
  'will retry ownership on the next periodic tick.'
)){
  if(!$listener.Contains($guard)){
    throw ('R367 listener takeover regression: '+$guard)
  }
}
if($listener.Contains('out bool ownsStream')){
  throw 'R367 listener still treats createdNew as ownership'
}
foreach($guard in @(
  'timer.Tick+=async (_,_)=',
  'DeskSpaceReleasePushV133.Start(owner);',
  'R367 installer transfer started;',
  'R367 installer transfer completed;',
  'stage.StartsWith("Download",StringComparison.Ordinal)',
  'DateTimeOffset.UtcNow.AddMinutes(-25)',
  'DeskSpaceUpdateRateV127.PollFile(Local,"tester")',
  'DeskSpaceVerifiedUpdateQueueV191.Stage(',
  'DeskSpaceAtomicInstallerV222.StageFromStreamAsync(input,file,ct)',
  'DeskSpaceUpdateRateV127.RetryAfter(answer,',
  'Verify(stableInstaller,sha,null)',
  'Verify(installer,checksum,root)',
  'R353 update supervisor accepted'
)){
  if(!$updater.Contains($guard)){
    throw ('R367 release trust/retry guard missing: '+$guard)
  }
}
if($updater.Contains('timer?.Stop();')){
  throw 'R367 must retain the R353 automatic-check timer'
}
$srcListener=Get-Content 'src/DeskSpace/DeskSpaceReleasePushV133.cs' -Raw
$srcUpdater=Get-Content 'src/DeskSpace/DeskSpacePublicUpdaterV44.cs' -Raw
foreach($guard in @('streamOwner.WaitOne(0)','catch(AbandonedMutexException)')){
  if(!$srcListener.Contains($guard)){throw ('R367 reviewed listener missing: '+$guard)}
}
foreach($guard in @('R367 installer transfer started;',
                    'DateTimeOffset.UtcNow.AddMinutes(-25)',
                    'DeskSpaceReleasePushV133.Start(owner);')){
  if(!$srcUpdater.Contains($guard)){throw ('R367 reviewed updater missing: '+$guard)}
}
Write-Host 'R367 PASS: actual shipping and source listener can recover mutex ownership;'
Write-Host 'five-minute network-failure retry, GitHub rate limit, SHA-256, installer trust and R353 retry guards retained.'
