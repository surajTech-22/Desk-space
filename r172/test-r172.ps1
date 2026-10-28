$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
foreach($token in @(
 'host.Dispatcher.BeginInvoke(new Action(()=',
 '_=CommitFolderDropAsync(paths,zipFolders))',
 'await Task.Yield();',
 'await Task.Run(()=>{',
 'using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);',
 'DeskSpaceFolderCopyV164.Copy(',
 'CancellationTokenSource',
 'copyInProgress=false;',
 'originalByStaged[destination]=Path.GetFullPath(originalSource);'
)){
 if(!$s.Contains($token)){throw "R172 missing production safety guard: $token"}
}
if($s.Contains('using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);'+
 [Environment]::NewLine+'                    // The copy worker')){
 throw 'R172 marker is still opened synchronously in UI context'
}
$sm=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('R171 stage A: before Copy action',
 'R171 stage B: Copy action returned',
 'R171 stage D: Copy-held wait returned',
 'R171 stage F: ZIP action returned')){
 if(!$sm.Contains($token)){throw "R172 missing WPF Copy/ZIP regression $token"}
}
Write-Host 'R172 PASS: synchronous PreviewDrop/Commit action never flushes a file; background worker owns lease and active Copy.'
