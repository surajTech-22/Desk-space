$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$component=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceComponentHealthV175.cs')
$signal=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceHealthSignalsV194.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($x in @(
    'DeskSpaceHealthSignalsV194.Search(',
    'DeskSpaceSearchIndexV13.IsIndexing',
    'DeskSpaceSearchIndexV13.Coverage',
    'new("Process resources",',
    'DeskSpaceHealthSignalsV194.CurrentMemory()'
)){
 if(!$component.Contains($x)){throw ("R194 status integration missing "+$x)}
}
foreach($x in @(
    'Process.GetCurrentProcess()',
    'process.WorkingSet64',
    'No indexed volumes reported',
    'Index warming',
    'Working set'
)){
 if(!$signal.Contains($x)){throw ("R194 diagnostics missing "+$x)}
}
if($signal.Contains('File.ReadAllText') -or $signal.Contains('Clipboard.') -or
   $signal.Contains('HttpClient') -or $signal.Contains('DispatcherTimer')){
 throw 'R194 must only report on-demand process counters and index status'
}
if(!$smoke.Contains('TestComponentSignalsV194();')){
 throw 'R194 native WPF status test missing'
}
Write-Host 'R194 PASS: on-demand search and resource status; no data reads or new timers.'
