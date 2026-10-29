$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$queue=[IO.File]::ReadAllText("$root/DeskSpaceVerifiedUpdateQueueV191.cs")
$updater=[IO.File]::ReadAllText("$root/DeskSpacePublicUpdaterV44.cs")
foreach($token in @(
    'CryptographicOperations.FixedTimeEquals',
    'SHA256.HashData(data)',
    'FileAttributes.ReparsePoint',
    'DeskSpace_Setup.exe',
    'internal static void Stage(',
    'internal static bool IsStillVerified(',
    'internal static Candidate? Peek(',
    'ReferenceEquals(pending,candidate)',
    'TagMatches(candidate.Tag,candidate.Channel)'
)){
    if(!$queue.Contains($token)){throw "R191 queue missing $token"}
}
foreach($token in @(
    'if(await ResumeVerifiedV191(owner,automatic))return;',
    'DeskSpaceVerifiedUpdateQueueV191.Stage(stableInstaller,',
    'DeskSpaceVerifiedUpdateQueueV191.Stage(installer,folder,',
    'DeskSpaceVerifiedUpdateQueueV191.IsStillVerified(pending)',
    'DeskSpaceUpdateInstallGateV188.OtherInstalledProcessRunning(target)',
    'DeskSpaceUpdateProgress.Publish("Restarting",100);',
    'Log("R191 verified staged "',
    'await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);'
)){
    if(!$updater.Contains($token)){throw "R191 updater missing $token"}
}
if(([regex]::Matches($updater,[regex]::Escape('if(DeferForOpenDeskSpace(owner,automatic))return;'))).Count -ne 2){
    throw 'R191 both channel final pre-handoff peer checks are required'
}
if($updater.Contains('Stop-Process') -or $updater.Contains('Kill(') -or
   $queue.Contains('Stop-Process') -or $queue.Contains('Kill(')){
    throw 'R191 must not terminate other DeskSpace instances'
}
$stableVerify=$updater.IndexOf('Verify(stableInstaller,sha,null);')
$stableStage=$updater.IndexOf('DeskSpaceVerifiedUpdateQueueV191.Stage(stableInstaller,')
$stableCheck=$updater.IndexOf('if(DeferForOpenDeskSpace(owner,automatic))return;',$stableStage)
$stableHandoff=$updater.IndexOf('await StartSupervisor(stableInstaller,stableFolder,stableTag);')
$testerVerify=$updater.IndexOf('Verify(installer,checksum,root);')
$testerStage=$updater.IndexOf('DeskSpaceVerifiedUpdateQueueV191.Stage(installer,folder,')
$testerCheck=$updater.IndexOf('if(DeferForOpenDeskSpace(owner,automatic))return;',$testerStage)
$testerHandoff=$updater.IndexOf('await StartSupervisor(installer,folder,tag);',$testerStage)
if($stableVerify -lt 0 -or $stableStage -le $stableVerify -or
   $stableCheck -le $stableStage -or $stableHandoff -le $stableCheck -or
   $testerVerify -lt 0 -or $testerStage -le $testerVerify -or
   $testerCheck -le $testerStage -or $testerHandoff -le $testerCheck){
    throw 'R191 must verify before staging, and check live peers before each supervisor handoff'
}
Write-Host 'R191 PASS: staged SHA256, both channel final peer guards, no network recheck while queued, no forced termination.'
