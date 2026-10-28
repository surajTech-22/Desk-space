$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$worker=[IO.File]::ReadAllText("$root/DeskSpaceInterruptedCopyV165.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
    'using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);',
    'DeskSpaceInterruptedCopyV165.PruneAbandoned(',
    'TimeSpan.FromHours(24)',
    'DeskSpaceFolderCopyV164.Copy(',
    'originalByStaged[destination]=Path.GetFullPath(originalSource);'
)){
    if(!$deck.Contains($token)){throw "R165 integration missing: $token"}
}
foreach($token in @(
    'FileShare.None,4096,FileOptions.WriteThrough',
    'handle.Flush(flushToDisk:true);',
    'Guid.TryParseExact(name,"N",out _)',
    'if(!IsEntirelyStale(partial,threshold))continue;',
    'File.GetLastWriteTimeUtc(entry)>threshold',
    'FileAttributes.ReparsePoint',
    'File.Delete(marker);',
    'File.Delete(partial)',
    'Directory.Delete(partial,true)',
    'return name;'
)){
    if(!$worker.Contains($token)){throw "R165 recovery safety check missing: $token"}
}
foreach($token in @(
    'TestInterruptedCopyRecoveryV165();',
    'private static void TestInterruptedCopyRecoveryV165()',
    'R165 recovery must not touch a locked active copy',
    'R165 recovery must never delete a successfully staged copy',
    'R165 startup cleanup must never change user source files'
)){
    if(!$smoke.Contains($token)){throw "R165 regression missing: $token"}
}
Write-Host 'R165 PASS: copy leases, conservative startup cleanup and WPF filesystem safety tests installed.'
