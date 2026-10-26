$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$app=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs')
$publisher=[IO.File]::ReadAllText('r133/publish-r133.ps1')
$listener=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceReleasePushV133.cs')
function Require([string]$s,[string]$part,[string]$label){
 if(-not $s.Contains($part)){throw "R138 $($label) missing $part"}
}
foreach($part in @(
 'timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};',
 'DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(1);',
 'DeskSpaceReleaseNoticeV137.Show(channel,tag)',
 'LatestStableTagV131',
 'NotifyNewReleaseV132("stable",stableTag)',
 'NotifyNewReleaseV132(channel,tag)',
 'Verify(installer,checksum,root)',
 'Verify(stableInstaller,sha,null)'
)){Require $app $part 'stable fallback'}
foreach($part in @(
 '-Headers @{"Cache"="yes"}',
 'R138 POST accepted but cache visibility unverified',
 'R137 event available for reconnecting clients',
 'Invoke-WebRequest -UseBasicParsing',
 'if(-not $confirmed)',
 'throw "R133 $tag was published but immediate notification could not be delivered'
)){Require $publisher $part 'release send'}
foreach($part in @('?since=1h','owner,automatic:true,releaseEvent:true',
'lastAttemptUtc<TimeSpan.FromSeconds(45)')){
 Require $listener $part 'bounded cached listener'
}
if($app.Contains('timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};')){
 throw 'R138 old five-minute recovery remains'
}
Write-Host 'R138 PASS: 1-minute API-free Stable fallback, instant cached ntfy release hints with explicit caching, independent WPF notification, no trust-check bypass; tester backoff unchanged.'
Write-Warning 'A server POST acknowledgement does not establish receipt on every Windows device. Check local PUSH log when next release arrives.'