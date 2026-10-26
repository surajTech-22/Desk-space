$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$receiver=[IO.File]::ReadAllText("$r/DeskSpaceReleasePushV133.cs")
$updater=[IO.File]::ReadAllText("$r/DeskSpacePublicUpdaterV44.cs")
$notice=[IO.File]::ReadAllText("$r/DeskSpaceReleaseNoticeV137.cs")
$publisher=[IO.File]::ReadAllText('r133/publish-r133.ps1')
$ui=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
function Need([string]$s,[string]$phrase,[string]$source){
 if(!$s.Contains($phrase)){throw "R137 missing $source: $phrase"}
}
foreach($k in @(
 '?since=1h',
 'Release-event stream connected; replaying last hour',
 'lastAttemptUtc<TimeSpan.FromSeconds(45)',
 'Release hint ',
 'owner,automatic:true,releaseEvent:true',
 'TryReleaseEvent(line,DeskSpacePublicUpdaterV44.Channel',
 'Release event ignored: different channel'
)){Need $receiver $k 'receiver'}
foreach($k in @(
 'DeskSpaceReleaseNoticeV137.Show(channel,tag);',
 'DeskSpaceUpdateProgress.Publish("Verified update "+tag,0)',
 'New release notice requested (WPF + Windows balloon):',
 'NotifyNewReleaseV132("stable",stableTag);',
 'NotifyNewReleaseV132(channel,tag);',
 'Verify(installer,checksum,root)',
 'Verify(stableInstaller,sha,null)',
 'StartSupervisor(installer,folder,tag)',
 'StartSupervisor(stableInstaller,stableFolder,stableTag)'
)){Need $updater $k 'GitHub updater'}
foreach($k in @('WindowStyle=WindowStyle.None','ShowActivated=false',
'Topmost=true','DispatcherTimer','ShowInTaskbar=false','window.Show()',
'catch(Exception)')){Need $notice $k 'non-modal desktop notice'}
foreach($k in @('R137 event available for reconnecting clients',
'Invoke-WebRequest -UseBasicParsing',
'if($event.id -eq $received.id','if(-not $confirmed)')){
 Need $publisher $k 'relay publish-receipt test'
}
foreach($k in @('TestReleaseNotificationV137();',
'cached release events parsed per channel','Cross-channel event must be ignored',
'Older replayed releases must not re-trigger updates')){
 Need $smoke $k 'Windows compiled smoke'
}
# User screenshot displays v1.5.747, a pre-existing old build. Guard
# against reintroducing its vertical name editor and COM table style bug.
foreach($k in @('pairedFormatNamesV132','pairedFormatNamesV132.Children.Add',
'collectionAndData','formatHeadV126.Children.Insert(0,pairedFormatNamesV132)')){
 Need $ui $k 'original grouped mapping editor'
}
if($receiver.Contains('?since=latest')){
 throw 'R137 subscriber still loses all events sent during reconnect'
}
if($notice.Contains('Process.Start(') -or $notice.Contains('CheckAsync(')){
 throw 'R137 notification must not launch installs or trust relay content'
}
Write-Host 'R137 STATIC PASS: cache-backed recovery, rate-bounded event checks, dual-channel publisher receipt and independent WPF/Windows notification; original Excel grouping layout preserved.'
Write-Warning 'Individual Windows device receipt still requires local PUSH log; GitHub publishing cannot prove a PC displayed the notice.'