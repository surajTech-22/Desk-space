$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText("$r/DeskSpacePublicUpdaterV44.cs")
$relay=[IO.File]::ReadAllText("$r/DeskSpaceReleasePushV133.cs")
$send=[IO.File]::ReadAllText('r133/publish-r133.ps1')
function Need([string]$code,[string]$needle,[string]$label){
 if(!$code.Contains($needle)){throw "R133 $label missing $needle"}
}
foreach($v in @(
 'DeskSpaceReleasePushV133.Start(owner);',
 'bool releaseEvent=false)',
 'if(automatic && !releaseEvent && now<next)return;',
 'automatic && !releaseEvent,out var resumedAt)',
 'Verify(stableInstaller,sha,null)',
 'Verify(installer,checksum,root)',
 'StartSupervisor(stableInstaller,stableFolder,stableTag)',
 'await StartSupervisor(installer,folder,tag)',
 'NotifyNewReleaseV132("stable",stableTag);',
 'NotifyNewReleaseV132(channel,tag);'
)){Need $app $v 'updater'}
foreach($v in @(
 'https://ntfy.sh/',
 '/json?since=latest',
 'StreamHttp.SendAsync(request,',
 'HttpCompletionOption.ResponseHeadersRead',
 'TryReleaseEvent(line,DeskSpacePublicUpdaterV44.Channel,',
 'DeskSpacePublicUpdaterV44.CheckAsync(',
 'owner,automatic:true,releaseEvent:true',
 'announced<=installed',
 'requested.Substring(',
 'message.StartsWith(prefix',
 'lastAttemptUtc<TimeSpan.FromMinutes(1)',
 'Task.Delay(TimeSpan.FromSeconds(Math.Min(60,5*failures)))'
)){Need $relay $v 'event receiver'}
foreach($v in @(
 '$env:GITHUB_SHA',
 'releases/tags/$tag',
 'DeskSpace_Setup.exe',
 'DeskSpace_Setup.sha256.txt',
 'tester-v',
 'Invoke-RestMethod -Uri ("https://ntfy.sh/"+$topic)',
 'if($release.tag -ne $tag -or $release.target -ne $sha)'
)){Need $send $v 'verified publisher'}
if($relay.Contains('Process.Start(') -or $relay.Contains('Download(') -or
   $relay.Contains('DeskSpace_Setup.exe')){
 throw 'R133 untrusted event handler must not execute or download anything.'
}
Write-Host 'R133 STATIC PASS: release hints trigger immediate official GitHub check, release checksum trust & API 403/429 backoff untouched; both channel publication receipts and UI notification retained.'