$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$scanner=[IO.File]::ReadAllText("$root/DeskSpaceUpdateInstallGateV188.cs")
$client=[IO.File]::ReadAllText("$root/DeskSpacePublicUpdaterV44.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
    'Process.GetProcessesByName("DeskSpace")',
    'peer.Id==thisPid',
    'peer.MainModule?.FileName',
    'return true;',
    'Path.GetFullPath(peerExe)',
    'Path.GetFullPath(targetExe)',
    'StringComparison.OrdinalIgnoreCase',
    'peer.Dispose()',
    'BlocksInstall(peer.Id,thisPid,peerExe,targetExe,readable)'
)){
 if(!$scanner.Contains($token)){throw "R188 process safety gate missing: $token"}
}
foreach($token in @(
    'private static bool DeferForOpenDeskSpace(Window owner,bool automatic)',
    'DeskSpaceUpdateInstallGateV188.OtherInstalledProcessRunning(executable)',
    'DateTime.UtcNow-lastBusyNoticeUtc>=TimeSpan.FromMinutes(45)',
    'if(!automatic)',
    'if(DeferForOpenDeskSpace(owner,automatic))return;',
    'timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};',
    'Log("R188 update deferred:',
    'StartSupervisor(installer,folder,tag)',
    'Verify(installer,checksum,root)'
)){
 if(!$client.Contains($token)){throw "R188 periodic update safety missing: $token"}
}
if(([regex]::Matches($client,
    'if\(DeferForOpenDeskSpace\(owner,automatic\)\)return;')).Count -ne 2){
 throw 'R188 requires explicit deferral before download AND before supervisor shutdown'
}
$index1=$client.IndexOf('if(DeferForOpenDeskSpace(owner,automatic))return;')
$download=$client.IndexOf('await Download(setup,installer,timeout.Token);')
$index2=$client.LastIndexOf('if(DeferForOpenDeskSpace(owner,automatic))return;')
$shutdown=$client.IndexOf('await StartSupervisor(installer,folder,tag);')
if($index1 -lt 0 -or $download -lt $index1 -or
   $index2 -lt $download -or $shutdown -lt $index2){
 throw 'R188 unsafe update: deferral gates must precede download and app shutdown'
}
if($scanner.Contains('Stop-Process') -or
   $scanner.Contains('Kill(') -or
   $scanner.Contains('CloseMainWindow(') -or
   $client.Contains('ForceTerminateOtherDeskSpace')){
 throw 'R188 must not silently terminate other application processes'
}
foreach($token in @(
    'TestSafeUpdateDeferralV188();',
    'private static void TestSafeUpdateDeferralV188()',
    'R188 a second DeskSpace process in same install must defer',
    'R188 a different installation must not block this one',
    'R188 inaccessible executable path must defer conservatively'
)){
 if(!$native.Contains($token)){throw "R188 native regression missing: $token"}
}
Write-Host 'R188 PASS: two non-destructive update deferral gates, existing one-minute fallback, throttled status and native peer isolation tests.'
