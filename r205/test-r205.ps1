$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$health=[IO.File]::ReadAllText("$root/DeskSpaceComponentHealthV175.cs")
$metrics=[IO.File]::ReadAllText("$root/DeskSpaceResourceSnapshotV205.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'Process CPU and handles',
 'DeskSpaceResourceSnapshotV205.Current()'
)){
 if(!$health.Contains($needle)){throw ("R205 UI integration missing "+$needle)}
}
foreach($needle in @(
 'TotalProcessorTime',
 'Process.GetCurrentProcess()',
 'GC.GetTotalMemory(false)',
 'process.HandleCount',
 'Environment.ProcessorCount',
 'Lifetime CPU mean',
 'Resource counters unavailable'
)){
 if(!$metrics.Contains($needle)){throw ("R205 counters missing "+$needle)}
}
if(!$native.Contains('TestResourceSnapshotV205();')){
 throw 'R205 native resource test absent'
}
if($metrics.Contains('DispatcherTimer') -or $metrics.Contains('File.Read') -or
   $metrics.Contains('HttpClient') -or $metrics.Contains('Task.Delay(')){
 throw 'R205 process-health sample must not schedule background polling or read files'
}
Write-Host 'R205 STATIC PASS: on-demand-only resource metrics, no telemetry and no timers.'
