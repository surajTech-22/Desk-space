$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMultiMonitorV151.cs'
$s=[IO.File]::ReadAllText($p)
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'new FileSystemWatcher(WorkspaceStore.Root,',
 'watcher.Changed+=(_,_)=>SchedulePreferenceRefreshV216();',
 'watcher.Created+=(_,_)=>SchedulePreferenceRefreshV216();',
 'watcher.Renamed+=(_,_)=>SchedulePreferenceRefreshV216();',
 'watcher.Deleted+=(_,_)=>SchedulePreferenceRefreshV216();',
 'watcher.Error+=(_,_)=>{',
 'Interval=TimeSpan.FromMilliseconds(180)',
 'Interval=ReconcileFallbackV216',
 'private static readonly TimeSpan ReconcileFallbackV216=TimeSpan.FromSeconds(45);',
 'StopPreferenceWatcherV216();',
 'preferenceWatcherV216?.Dispose();'
)){
 if(!$s.Contains($needle)){throw ("R216 event preference refresh missing "+$needle)}
}
if($s.Contains('Interval=TimeSpan.FromSeconds(3)')){
 throw 'R216 continuous three-second monitor polling must be eliminated'
}
if(!$t.Contains('TestEventDrivenDisplaysV216();')){
 throw 'R216 native WPF test not registered'
}
Write-Host 'R216 STATIC PASS: display settings react to OS/file events, overflow backup and clean unsubscribe.'
